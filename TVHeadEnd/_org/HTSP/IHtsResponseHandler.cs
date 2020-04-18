namespace TVHeadEnd.HTSP
{
    public interface IHtsResponseHandler
    {
        void HandleResponse(HtsMessage response);
    }
}