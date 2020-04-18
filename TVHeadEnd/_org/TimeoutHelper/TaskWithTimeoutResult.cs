namespace TVHeadEnd.TimeoutHelper
{
    public class TaskWithTimeoutResult<T>
    {
        public T Result { get; set; }
        public bool HasTimeout { get; set; }
    }
}