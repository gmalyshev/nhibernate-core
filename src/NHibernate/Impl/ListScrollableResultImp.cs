using System;
using System.Collections;
using System.Collections.Generic;
using System.Transactions;
using NHibernate.Engine;
using NHibernate.SqlCommand;
using NHibernate.Type;

namespace NHibernate.Impl
{
    /// <summary>
    /// Using for to composit many IScrollableResults
    /// </summary>
    internal sealed class ListScrollableResultImp:IScrollableResults
    {

        class ExternalSessionIntercepter : IInterceptor
        {
            private readonly IInterceptor _interceptor;

            public ExternalSessionIntercepter(IInterceptor interceptor)
            {
                _interceptor = interceptor;
            }

            bool IInterceptor.OnLoad(object entity, object id, object[] state, string[] propertyNames, IType[] types)
            {
                return _interceptor.OnLoad(entity, id, state, propertyNames, types);
            }

            bool IInterceptor.OnFlushDirty(object entity, object id, object[] currentState, object[] previousState, string[] propertyNames,
                IType[] types)
            {
                return _interceptor.OnFlushDirty(entity, id, currentState, previousState, propertyNames, types);
            }

            bool IInterceptor.OnSave(object entity, object id, object[] state, string[] propertyNames, IType[] types)
            {
                return _interceptor.OnSave(entity, id, state, propertyNames, types);
            }

            void IInterceptor.OnDelete(object entity, object id, object[] state, string[] propertyNames, IType[] types)
            {
                 _interceptor.OnDelete(entity, id, state, propertyNames, types);
            }

            void IInterceptor.OnCollectionRecreate(object collection, object key)
            {
                _interceptor.OnCollectionRecreate(collection,key);
            }

            void IInterceptor.OnCollectionRemove(object collection, object key)
            {
                _interceptor.OnCollectionRemove(collection, key);
            }

            void IInterceptor.OnCollectionUpdate(object collection, object key)
            {
                _interceptor.OnCollectionUpdate(collection, key);
            }

            void IInterceptor.PreFlush(ICollection entities)
            {
                _interceptor.PreFlush(entities);
            }

            void IInterceptor.PostFlush(ICollection entities)
            {
               _interceptor.PostFlush(entities);
            }

            bool? IInterceptor.IsTransient(object entity)
            {
                return _interceptor.IsTransient(entity);
            }

            int[] IInterceptor.FindDirty(object entity, object id, object[] currentState, object[] previousState, string[] propertyNames,
                IType[] types)
            {
                return _interceptor.FindDirty(entity, id, currentState, previousState, propertyNames, types);
            }

            object IInterceptor.Instantiate(string entityName, EntityMode entityMode, object id)
            {
                return _interceptor.Instantiate(entityName, entityMode, id);
            }

            string IInterceptor.GetEntityName(object entity)
            {
                return _interceptor.GetEntityName(entity);
            }

            object IInterceptor.GetEntity(string entityName, object id)
            {
                return _interceptor.GetEntity(entityName,id);
            }

            void IInterceptor.AfterTransactionBegin(ITransaction tx)
            {
                _interceptor.AfterTransactionBegin(tx);
            }

            void IInterceptor.BeforeTransactionCompletion(ITransaction tx)
            {
               _interceptor.BeforeTransactionCompletion(tx);
            }

            void IInterceptor.AfterTransactionCompletion(ITransaction tx)
            {
                _interceptor.AfterTransactionCompletion(tx);
            }

            SqlString IInterceptor.OnPrepareStatement(SqlString sql)
            {
              return  _interceptor.OnPrepareStatement(sql);
            }

            void IInterceptor.SetSession(ISession session)
            {
                //nothing to do
            }
        }
        private readonly ISessionFactoryImplementor _sessionFactory;
        readonly List<IScrollableResults> _scrollableResultses = new List<IScrollableResults>();
        private int currentInList = 0;
        private ISessionImplementor _sessionImpl;        
        private bool _opened = true;

     
        public ListScrollableResultImp(ISessionImplementor sessionImplementor, ISessionFactoryImplementor sessionFactory)
        {
            _sessionFactory = sessionFactory;
            _sessionImpl = _sessionFactory.OpenSession(new ExternalSessionIntercepter(sessionImplementor.Interceptor)).GetSessionImplementation();           
        }

        public ISessionImplementor GetSession()
        {
            return _sessionImpl;
        }
        public IList<IScrollableResults>  ScrollableResultses
        {
            get { return _scrollableResultses; }
        }

        public object Get()
        {
            return _scrollableResultses[currentInList].Get();
        }

        public T Get<T>()
        {
            return _scrollableResultses[currentInList].Get<T>();
        }

        public void Close()
        {
            foreach (var scrollableResultse in _scrollableResultses)
            {
                scrollableResultse.Close();
            }

           ((IDisposable) _sessionImpl).Dispose();
            _opened = false;
        }

        public bool Next()
        {
            if (!_opened) return false;
            if (_scrollableResultses[currentInList].Next())
                return true;
            if (currentInList < _scrollableResultses.Count - 1)
            {
                currentInList++;
                return Next();
            }
            Close();
            return false;
        }

        public bool Scroll(int countNext)
        {
            if (countNext<=0)throw new ArgumentOutOfRangeException();
            bool result = true;
            while (countNext>0)
            {
                result = result && Next();
                countNext--;
            }

            return result;
        }

        void IDisposable.Dispose()
        {
           Close();
        }
    }
}