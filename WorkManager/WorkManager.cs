//#define CODE_COVERAGE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace App
{
	
#if !CODE_COVERAGE
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
    class WorkManager<SHARED, WORK> where WORK : class 
    {
        private object _locker = new object();
        private Worker<SHARED, WORK>[] _workers;
        private IEnumerator<WORK> _enumerator;

        private WorkManager() { }

        private WORK GetNext()
        {
            lock (_locker)
            {
                if (_enumerator.MoveNext())
                {
                    return _enumerator.Current;
                }
                else
                    return default(WORK);
            }
        }

        static public Task Process(int numberOfInstances,
            Func<Task<SHARED>> getShared, Func<SHARED, Task> completeShared,
            IEnumerable<WORK> work, Func<SHARED, WORK, Task> processItem)
        {
            var wm = new WorkManager<SHARED, WORK>()
            {
                _enumerator = work.GetEnumerator(),
            };

            wm._workers = Enumerable.Range(0, numberOfInstances)
                        .Select(i => new Worker<SHARED, WORK>(getShared, completeShared, wm, processItem))
                        .ToArray();

            var tasks = wm._workers.Select(w => w.DoWork()).ToArray();

            return Task.WhenAll(tasks);
        }

#if !CODE_COVERAGE
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
        class Worker<S, W> where W : class
        {
            private Func<Task<S>> _getShared;
            private Func<S, Task> _completedShared;
            private WorkManager<S, W> _man;
            private Func<S, W, Task> _processItem;
            private S _sharedItem;

            public Worker(Func<Task<S>> getShared, Func<S, Task> completedShared, WorkManager<S, W> man, Func<S, W, Task> processItem)
            {
                _man = man;
                _getShared = getShared;
                _completedShared = completedShared;
                _processItem = processItem;
            }

            public async Task DoWork()
            {
                _sharedItem = await _getShared();

                try
                {
                    var work = _man.GetNext();

                    while (work != null)
                    {
                        await _processItem(_sharedItem, work);

                        work = _man.GetNext();
                    }
                }
                finally
                {
                    await _completedShared(_sharedItem);
                }
            }
        }
    }
}