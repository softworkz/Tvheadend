namespace TVHeadEnd.Helper
{
    using System.IO;

    public class ByteBuffer
    {
        private readonly MemoryStream stream;
        private readonly BinaryReader reader;
        private readonly BinaryWriter writer;

        public ByteBuffer(byte[] data)
        {
            this.stream = new MemoryStream();
            this.reader = new BinaryReader(this.stream);
            this.writer = new BinaryWriter(this.stream);
            this.writer.Write(data);
            this.stream.Position = 0;
        }

        ~ByteBuffer()
        {
            this.reader.Close();
            this.writer.Close();
            this.stream.Close();
            this.stream.Dispose();
        }

        public long Length()
        {
            return this.stream.Length;
        }

        public bool HasRemaining()
        {
            return this.stream.Length - this.stream.Position > 0;
        }

        public byte Get()
        {
            return (byte)this.stream.ReadByte();
        }

        public void Get(byte[] dst)
        {
            this.stream.Read(dst, 0, dst.Length);
        }
    }
}