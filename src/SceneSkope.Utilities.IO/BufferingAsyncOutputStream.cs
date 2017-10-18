using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.IO
{
    public class BufferingAsyncOutputStream : Stream
    {
        private class Buffer
        {
            public byte[] Array { get; }
            public int Position { get; set; }
            public bool Finished => Position == Array.Length;

            public Buffer(int bufferSize)
            {
                Array = ArrayPool<byte>.Shared.Rent(bufferSize);
            }
        }

        private readonly Stream _outputStream;
        private readonly Buffer[] _buffers;
        private readonly bool _leaveOpen;

        private int _readingBuffer;
        private int _writingBuffer;
        private int _fullBuffers;
        private bool _errored;

        public BufferingAsyncOutputStream(Stream outputStream, int bufferSize = 8192, bool leaveOpen = false)
        {
            _outputStream = outputStream;
            _buffers = new[]
            {
                new Buffer(bufferSize),
                new Buffer(bufferSize)
            };
            _leaveOpen = leaveOpen;
        }

        public async Task CloseAsync(CancellationToken ct)
        {
            await EmptyAsync(ct).ConfigureAwait(false);
            var activeBuffer = _buffers[_writingBuffer];
            if (activeBuffer.Position > 0)
            {
                await _outputStream.WriteAsync(activeBuffer.Array, 0, activeBuffer.Position, ct).ConfigureAwait(false);
                activeBuffer.Position = 0;
            }
            Dispose();
        }

        public async Task EmptyAsync(CancellationToken ct)
        {
            while (NeedsEmptying)
            {
                var buffer = _buffers[_readingBuffer];
                await _outputStream.WriteAsync(buffer.Array, 0, buffer.Position, ct).ConfigureAwait(false);
                buffer.Position = 0;
                _readingBuffer = (_readingBuffer + 1) % _buffers.Length;
                _fullBuffers--;
            }
        }

        public bool NeedsEmptying => _fullBuffers > 0;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _position;

        private long _position;
        public override long Position { get => _position; set => throw new IOException("Stream not seekable"); }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => EmptyAsync(cancellationToken);

        public int LastCount { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            LastCount = count;
            while (count > 0)
            {
                var activeBuffer = _buffers[_writingBuffer];
                var available = activeBuffer.Array.Length - activeBuffer.Position;
                if (available == 0)
                {
                    _errored = true;
                    throw new IOException("Buffers need emptying");
                }
                var bytesToWrite = count > available ? available : count;
                System.Buffer.BlockCopy(buffer, offset, activeBuffer.Array, activeBuffer.Position, bytesToWrite);
                activeBuffer.Position += bytesToWrite;
                offset += bytesToWrite;
                _position += bytesToWrite;
                count -= bytesToWrite;

                if (activeBuffer.Finished)
                {
                    _writingBuffer = (_writingBuffer + 1) % _buffers.Length;
                    _fullBuffers++;
                    if ((count > 0) && (_fullBuffers == _buffers.Length))
                    {
                        _errored = true;
                        throw new IOException("Buffer size too small, reading and writing buffers the same");
                    }
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Stream not readable");

        public override long Seek(long offset, SeekOrigin origin) => throw new IOException("Stream not writable");

        public override void SetLength(long value) => throw new IOException("Stream not writable");

        protected override void Dispose(bool disposing)
        {
            CheckedDispose();
            if (!_leaveOpen)
            {
                _outputStream.Dispose();
            }
            base.Dispose(disposing);
        }

        private void CheckedDispose()
        {
            var shouldThrow = false;
            for (var i = 0; i < _buffers.Length; i++)
            {
                var buffer = _buffers[i];
                var array = buffer?.Array;
                if (array != null)
                {
                    if (buffer.Position != 0)
                    {
                        shouldThrow = true;
                    }
                    ArrayPool<byte>.Shared.Return(array);
                    _buffers[i] = null;
                }
            }
            if (shouldThrow && !_errored)
            {
                throw new IOException("Buffers are not empty");
            }
        }
    }
}
