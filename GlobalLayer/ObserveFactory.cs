using GlobalLayer;
using System;
using System.Collections.Generic;

namespace GlobalLayer
{
    public class ObservableFactory : IObservable<Tick>
    {
        ///<summary>
        /// List of subscribers to this ticker
        ///</summary>
        public List<IObserver<Tick>> observers = new List<IObserver<Tick>>();
        private IDisposable unsubscriber;
        public IDisposable Subscribe(IObserver<Tick> observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }
            return new Unsubscriber(observers, observer);
        }
        public virtual void Unsubscribe()
        {
            unsubscriber.Dispose();
        }
    }
    class Unsubscriber : IDisposable
    {
        private List<IObserver<Tick>> _observers;
        private IObserver<Tick> _observer;

        public Unsubscriber(List<IObserver<Tick>> observers, IObserver<Tick> observer)
        {
            this._observers = observers;
            this._observer = observer;
        }

        public void Dispose()
        {
            if (!(_observer == null)) _observers.Remove(_observer);
        }
    }
}
