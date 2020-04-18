namespace TVHeadEnd.HTSP.Responses
{
    using TVHeadEnd.Helper;

    public class LoopBackResponseHandler : IHtsResponseHandler
    {
        private readonly SizeQueue<HtsMessage> responseDataQueue;

        public LoopBackResponseHandler()
        {
            this.responseDataQueue = new SizeQueue<HtsMessage>(1);
        }

        public void HandleResponse(HtsMessage response)
        {
            this.responseDataQueue.Enqueue(response);
        }

        public HtsMessage GetResponse()
        {
            return this.responseDataQueue.Dequeue();
        }
    }
}