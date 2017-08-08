using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SceneSkope.Utilities.IO
{
    public class BufferingAsyncInputStream : Stream
    {
        public static async Task<BufferingAsyncInputStream> CreateAndFillAsync(Stream inputStream, int bufferSize, CancellationToken ct)
        {
            var stream = new BufferingAsyncInputStream(inputStream, bufferSize);
            while (stream.NeedsFilling)
            {
                await stream.FillAsync(ct).ConfigureAwait(false);
            }
            return stream;
        }

        private class Buffer
        {
            public byte[] Array { get; }
            public int Length { get; set; }
            public int Position { get; set; }
            public bool Finished => Position == Length;

            public Buffer(int bufferSize)
            {
                Array = ArrayPool<byte>.Shared.Rent(bufferSize);
            }
        }

        private readonly Stream _inputStream;
        private readonly Buffer[] _buffers;
        private readonly bool _leaveOpen;

        private int _readingBuffer;
        private int _writingBuffer;
        private int _emptyBuffers;

        public BufferingAsyncInputStream(Stream inputStream, int bufferSize = 8192, bool leaveOpen = false)
        {
            _inputStream = inputStream;
            _buffers = new[]
            {
                new Buffer(bufferSize),
                new Buffer(bufferSize)
            };
            _emptyBuffers = _buffers.Length;
            _leaveOpen = leaveOpen;
        }

        public async Task FillAsync(CancellationToken ct)
        {
            while ((_emptyBuffers > 0) && (_readingBuffer != -1))
            {
                var buffer = _buffers[_writingBuffer];
                var bytesRead = await _inputStream.ReadAsync(buffer.Array, 0, buffer.Array.Length, ct).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    _emptyBuffers = -1;
                    var activeBuffer = _buffers[_readingBuffer];
                    if (activeBuffer.Finished)
                    {
                        _readingBuffer = -1;
                    }
                }
                else
                {
                    buffer.Length = (int)bytesRead;
                    buffer.Position = 0;
                    _writingBuffer = (_writingBuffer + 1) % _buffers.Length;
                    _emptyBuffers--;
                }
            }
        }

        public bool NeedsFilling => _emptyBuffers > 0;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _inputStream.Length;

        private long _position;
        public override long Position { get => _position; set => throw new IOException("Stream not writable"); }

        public override void Flush() => throw new IOException("Stream not writable");

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_readingBuffer == -1)
            {
                return 0;
            }
            var activeBuffer = _buffers[_readingBuffer];
            if (activeBuffer.Finished)
            {
                if (_emptyBuffers == -1)
                {
                    return 0;
                }
                else
                {
                    throw new IOException("Stream has not been filled");
                }
            }
            var available = activeBuffer.Length - activeBuffer.Position;
            if (count > available)
            {
                count = available;
            }
            System.Buffer.BlockCopy(activeBuffer.Array, activeBuffer.Position, buffer, offset, count);
            activeBuffer.Position += count;
            _position += count;
            if (activeBuffer.Finished)
            {
                if (_emptyBuffers == -1)
                {
                    _readingBuffer = -1;
                }
                else
                {
                    _emptyBuffers++;
                    _readingBuffer = (_readingBuffer + 1) % _buffers.Length;
                }
            }
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new IOException("Stream not writable");

        public override void SetLength(long value) => throw new IOException("Stream not writable");

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("Stream not writable");

        protected override void Dispose(bool disposing)
        {
            for (var i = 0; i < _buffers.Length; i++)
            {
                var array = _buffers[i]?.Array;
                if (array != null)
                {
                    ArrayPool<byte>.Shared.Return(array);
                    _buffers[i] = null;
                }
            }
            if (!_leaveOpen)
            {
                _inputStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
