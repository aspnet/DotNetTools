// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.SecretManager
{
    /// <summary>
    /// Provides read and write access to the secrets.json file for local user secrets.
    /// This is not thread-safe.
    /// This object is meant to have a short lifetime.
    /// When calling <see cref="SaveAsync(CancellationToken)"/>, this will overwrite the secrets.json file. It does not check for concurrency issues if another process has edited this file.
    /// </summary>
    internal class SecretStore : IDisposable
    {
        private Dictionary<string, string> _secrets;
        private IVsInvisibleEditor _editor;
        private string _path;
        private Shell.RunningDocumentTable _rdt;
        private object _document;
        private bool _isDirty;
        private volatile bool _disposed;
        private readonly Lazy<IServiceProvider> _services;

        public SecretStore(string userSecretsId, Lazy<IServiceProvider> services)
        {
            _services = services;
            _path = PathHelper.GetSecretsPathFromSecretsId(userSecretsId);
        }

        public IReadOnlyCollection<string> ReadOnlyKeys
        {
            get
            {
                EnsureNotDisposed();
                return _secrets.Keys;
            }
        }

        public IReadOnlyDictionary<string, string> Values
        {
            get
            {
                EnsureNotDisposed();

                return _secrets;
            }
        }

        public bool ContainsKey(string key)
        {
            EnsureNotDisposed();

            return _secrets.ContainsKey(key);
        }

        public string Get(string name)
        {
            EnsureNotDisposed();

            return _secrets[name];
        }

        public void Set(string key, string value)
        {
            EnsureNotDisposed();

            _isDirty = true;
            _secrets[key] = value;
        }

        public bool Remove(string key)
        {
            EnsureNotDisposed();
            _isDirty = true;
            return _secrets.Remove(key);
        }

        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureNotDisposed();

            string text = null;

            _rdt = new Shell.RunningDocumentTable(_services.Value);
            _document = _rdt.FindDocument(_path);

            await Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (_document is IVsFullTextScanner fullText)
            {
                text = ReadFullText(fullText);
            }

            if (text == null)
            {
                var invisibleEditorManager = (IVsInvisibleEditorManager)_services.Value.GetService(typeof(SVsInvisibleEditorManager));

                int hr = invisibleEditorManager.RegisterInvisibleEditor(_path, null, 0, null, out _editor);
                Marshal.ThrowExceptionForHR(hr);

                text = GetTextFromInvisibleEditor(_editor);
            }

            await TaskScheduler.Default;

            _secrets = DeserializeJson(text);
        }

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnsureNotDisposed();


            if (!_isDirty)
            {
                return;
            }

            await TaskScheduler.Default;

            var contents = Stringify(_secrets);

            await Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (_editor != null)
            {
                SaveTextToInvisibleEditor(_editor, contents);
            }
            else
            {
                SaveTextToBuffer((ITextBuffer)_document, contents);
            }

            _rdt.SaveFileIfDirty(_path);

            _isDirty = false;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SecretStore));
            }
        }

        private static string Stringify(Dictionary<string, string> secrets)
        {
            var contents = new JObject();
            if (secrets != null)
            {
                foreach (var secret in secrets.AsEnumerable())
                {
                    contents[secret.Key] = secret.Value;
                }
            }

            return contents.ToString();
        }

        private static Dictionary<string, string> DeserializeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var provider = new JsonConfigurationProvider(new JsonConfigurationSource());
            using (var stream = new MemoryStream())
            {
                var bytes = Encoding.Unicode.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
                stream.Position = 0;
                // might throw FormatException if JSON is malformed. 
                provider.Load(stream);
            }

            var root = new ConfigurationRoot(new[] { provider });

            return root
                .AsEnumerable()
                .Where(v => v.Value != null)
                .ToDictionary(i => i.Key, i => i.Value, StringComparer.OrdinalIgnoreCase);
        }

        private string GetTextFromInvisibleEditor(IVsInvisibleEditor editor)
        {
            var docDataPtr = IntPtr.Zero;
            try
            {
                int hr = editor.GetDocData(0, typeof(IVsFullTextScanner).GUID, out docDataPtr);
                Marshal.ThrowExceptionForHR(hr);

                var fullText = (IVsFullTextScanner)Marshal.GetObjectForIUnknown(docDataPtr);

                return ReadFullText(fullText);
            }
            finally
            {
                Marshal.Release(docDataPtr);
            }
        }

        private void SaveTextToInvisibleEditor(IVsInvisibleEditor editor, string text)
        {
            var docDataPtr = IntPtr.Zero;
            try
            {
                int hr = editor.GetDocData(0, typeof(IVsFullTextScanner).GUID, out docDataPtr);
                Marshal.ThrowExceptionForHR(hr);

                var buffer = (ITextBuffer)Marshal.GetObjectForIUnknown(docDataPtr);
                SaveTextToBuffer(buffer, text);
            }
            finally
            {
                Marshal.Release(docDataPtr);
            }
        }

        private static void SaveTextToBuffer(ITextBuffer textBuffer, string text)
        {
            textBuffer.Replace(Span.FromBounds(0, textBuffer.CurrentSnapshot.Length), text);
        }

        private static string ReadFullText(IVsFullTextScanner fullText)
        {
            Debug.Assert(fullText != null);

            Marshal.ThrowExceptionForHR(fullText.OpenFullTextScan());
            Marshal.ThrowExceptionForHR(fullText.FullTextRead(out string text, out int _));
            Marshal.ThrowExceptionForHR(fullText.CloseFullTextScan());
            return text;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            if (_editor != null)
            {
                Marshal.ReleaseComObject(_editor);
            }

            _editor = null;
            _document = null;
        }
    }
}
