using System.Collections.Generic;

namespace SPFServer.Main
{
    public delegate void ReturnedResult<T>(T obj);

    public class CallbackManager<T>
    {
        private Dictionary<int, ReturnedResult<T>> callbacks = 
            new Dictionary<int, ReturnedResult<T>>();

        public Dictionary<int, ReturnedResult<T>> Callbacks {  get { return callbacks; } }

        public CallbackManager()
        {
        }

        public void AddCallback(int key, ReturnedResult<T> callback)
        {
            callbacks.Add(key, callback);
        }

        public void InvokeCallbackByID(int id, T obj)
        {
            ReturnedResult<T> result;
            if (callbacks.TryGetValue(id, out result))
            {
                result.Invoke(obj);
                callbacks.Remove(id);
            }
        }
    }
}
