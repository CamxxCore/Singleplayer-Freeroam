using System.Collections.Generic;
using System.Threading;
using System.ComponentModel;
using System;

namespace SPFServer
{
    public class ThreadQueue
    {
        private readonly HashSet<ThreadStart> WorkingThreads = new HashSet<ThreadStart>();
        private readonly Queue<ThreadStart> Queue = new Queue<ThreadStart>();

        private bool RaiseCompleteEventIfQueueEmpty = false;

        private int ThreadsMaxCount;

        public ThreadQueue(int threadsMaxCount)
        {
            ThreadsMaxCount = threadsMaxCount;
        }

        /// <summary>
        /// Creates a new thread queue with a maximum number of threads and the tasks that should be executed.
        /// </summary>
        /// <param name="threadsMaxCount">The maximum number of currently active threads.</param>
        /// <param name="tasks">The tasks that should be executed by the queue.</param>
        public ThreadQueue(int threadsMaxCount, ThreadStart[] tasks) : this(threadsMaxCount)
        {
            RaiseCompleteEventIfQueueEmpty = true;
            foreach (ThreadStart task in tasks)
            {
                Queue.Enqueue(task);
            }
        }

        /// <summary>
        /// Starts to execute tasks. Used in conjunction with the constructor in which all tasks are provided.
        /// </summary>
        public void Start()
        {
            CheckQueue();
        }

        private readonly object addlock = new object();
        /// <summary>
        /// Adds a task and runs it if a execution slot is free. Otherwise it will be enqueued.
        /// </summary>
        /// <param name="task">The task that should be executed.</param>
        public void AddTask(ThreadStart task)
        {
            lock (addlock)
            {
                if (WorkingThreads.Count == ThreadsMaxCount)
                {
                    Queue.Enqueue(task);
                }
                else
                {
                    StartThread(task);
                }
            }
        }

        /// <summary>
        /// Starts the execution of a task.
        /// </summary>
        /// <param name="task">The task that should be executed.</param>
        private void StartThread(ThreadStart task)
        {
            WorkingThreads.Add(task);
            BackgroundWorker thread = new BackgroundWorker();
            thread.DoWork += delegate { task.Invoke(); };
            thread.RunWorkerCompleted += delegate { ThreadCompleted(task); };
            thread.RunWorkerAsync();
        }

        private void ThreadCompleted(ThreadStart start)
        {
            WorkingThreads.Remove(start);
            CheckQueue();
            if (Queue.Count == 0 && WorkingThreads.Count == 0 && RaiseCompleteEventIfQueueEmpty) OnCompleted();
        }

        private readonly object checklock = new object();
        /// <summary>
        /// Checks if the queue contains tasks and runs as many as there are free execution slots.
        /// </summary>
        private void CheckQueue()
        {
            lock (checklock)
            {
                while (Queue.Count > 0 && WorkingThreads.Count < ThreadsMaxCount)
                {
                    StartThread(Queue.Dequeue());
                }
                if (Queue.Count == 0 && WorkingThreads.Count == 0 && RaiseCompleteEventIfQueueEmpty) OnCompleted();
            }
        }

        /// <summary>
        /// Raised when all tasks have been completed. 
        /// Will only be used if the ThreadQueue has been initialized with all the tasks it should execute.
        /// </summary>
        public event EventHandler Completed;

        /// <summary>
        /// Raises the Completed event.
        /// </summary>
        protected void OnCompleted()
        {
            if (Completed != null)
            {
                Completed(this, null);
            }
        }
    }
}