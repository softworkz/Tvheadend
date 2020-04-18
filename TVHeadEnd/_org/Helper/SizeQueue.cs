namespace TVHeadEnd.Helper
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public class SizeQueue<T>
    {
        private readonly TimeSpan timeOut = new TimeSpan(0, 0, 30);
        private readonly Queue<T> queue = new Queue<T>();
        private readonly int maxSize;

        public SizeQueue(int maxSize)
        {
            this.maxSize = maxSize;
        }

        public void Enqueue(T item)
        {
            lock (this.queue)
            {
                while (this.queue.Count >= this.maxSize)
                {
                    Monitor.Wait(this.queue, this.timeOut);
                }

                this.queue.Enqueue(item);
                if (this.queue.Count == 1)
                {
                    // wake up any blocked dequeue
                    Monitor.PulseAll(this.queue);
                }
            }
        }

        public T Dequeue()
        {
            lock (this.queue)
            {
                while (this.queue.Count == 0)
                {
                    Monitor.Wait(this.queue, this.timeOut);
                }

                T item = this.queue.Dequeue();
                if (this.queue.Count == this.maxSize - 1)
                {
                    // wake up any blocked enqueue
                    Monitor.PulseAll(this.queue);
                }

                return item;
            }
        }
    }
}