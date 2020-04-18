namespace TVHeadEnd.Helper
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public class ByteList
    {
        private readonly List<byte> data;

        public ByteList()
        {
            this.data = new List<byte>();
        }

        public byte[] GetFromStart(int count)
        {
            lock (this.data)
            {
                while (this.data.Count < count)
                {
                    Monitor.Wait(this.data);
                }

                return this.data.GetRange(0, count).ToArray();
            }
        }

        public byte[] ExtractFromStart(int count)
        {
            lock (this.data)
            {
                while (this.data.Count < count)
                {
                    Monitor.Wait(this.data);
                }

                byte[] result = this.data.GetRange(0, count).ToArray();
                this.data.RemoveRange(0, count);
                return result;
            }
        }

        public void AppendAll(byte[] data)
        {
            lock (this.data)
            {
                this.data.AddRange(data);
                if (this.data.Count >= 1)
                {
                    // wake up any blocked dequeue
                    Monitor.PulseAll(this.data);
                }
            }
        }

        public void AppendCount(byte[] data, long count)
        {
            lock (this.data)
            {
                byte[] dataRange = new byte[count];
                Array.Copy(data, 0, dataRange, 0, dataRange.Length);
                this.AppendAll(dataRange);
            }
        }

        public int Count()
        {
            lock (this.data)
            {
                return this.data.Count;
            }
        }
    }
}