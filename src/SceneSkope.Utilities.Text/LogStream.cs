using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.Text
{
    public class LogStream : IDisposable
    {
        private readonly StreamReader _reader;
        private long _position;
        private int _lineNumber;
        private readonly char[] _buffer = new char[4096];
        private char[] _lineBuffer = new char[1024];
        private int _bufferPosition;
        private int _bufferLength;
        private int _linePosition;
        private bool _configured;

        public long Position => _position;
        public int LineNumber => _lineNumber;

        public LogStream(Stream stream, long? position = null, int? lineNumber = null)
        {
            _reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            if (position.HasValue)
            {
                _position = position.Value;
                stream.Seek(_position, SeekOrigin.Begin);
                _configured = true;
            }
            _lineNumber = lineNumber ?? 0;
        }

        private async Task<bool> TryFillBufferAsync(CancellationToken ct)
        {
            var charactersRead = await _reader.ReadBlockAsync(_buffer, 0, _buffer.Length).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            _bufferPosition = 0;
            if (charactersRead == 0)
            {
                _bufferLength = 0;
                return false;
            }
            else
            {
                if (!_configured)
                {
                    var bytesCount = _reader.CurrentEncoding.GetByteCount(_buffer, 0, charactersRead);
                    _position = _reader.BaseStream.Position - bytesCount;
                    _configured = true;
                }
                _bufferLength = charactersRead;
                return true;
            }
        }

        public async Task<(string line, int lineNumber)> TryReadNextLineAsync(CancellationToken ct, bool dontNeedNewLine = false)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (_bufferLength == _bufferPosition)
                {
                    var gotMoreData = await TryFillBufferAsync(ct).ConfigureAwait(false);
                    if (!gotMoreData)
                    {
                        if (dontNeedNewLine && (_linePosition > 0))
                        {
                            return CompleteLine();
                        }
                        else
                        {
                            return (null, -1);
                        }
                    }
                }

                while (_bufferPosition < _bufferLength)
                {
                    var ch = _buffer[_bufferPosition++];
                    if ((ch == '\n') || (ch == '\r'))
                    {
                        _position++;
                        if (_linePosition > 0)
                        {
                            return CompleteLine();
                        }
                    }
                    else
                    {
                        if (_linePosition == _lineBuffer.Length)
                        {
                            var newLineBuffer = new char[_lineBuffer.Length * 2];
                            Buffer.BlockCopy(_lineBuffer, 0, newLineBuffer, 0, _lineBuffer.Length * sizeof(char));
                            _lineBuffer = newLineBuffer;
                        }
                        _lineBuffer[_linePosition++] = ch;
                    }
                }
            }
        }

        private (string line, int lineNumber) CompleteLine()
        {
            var line = new string(_lineBuffer, 0, _linePosition);
            var bytesCount = _reader.CurrentEncoding.GetByteCount(line);
            _position += bytesCount;
            _linePosition = 0;
            _lineNumber++;
            return (line, _lineNumber);
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
