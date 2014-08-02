using System;
using System.Data;
using NHibernate.Engine;
using NHibernate.Loader.Criteria;

namespace NHibernate.Impl
{
    internal class ExternalDataReaderScrollableResultImp:IScrollableResults
    {
        private readonly ISessionImplementor _session;
        private readonly ISessionImplementor _externalsessionImpl;
        private readonly IDataReader _rs;
        private readonly int _maxRows;
        private readonly CriteriaLoader _criteriaLoader;
        private int _position = -1;
        private bool _opened = true;

        public ExternalDataReaderScrollableResultImp(ISessionImplementor session, ISessionImplementor externalsessionImpl, IDataReader rs, int maxRows, CriteriaLoader criteriaLoader)
        {
            _session = session;
            _externalsessionImpl = externalsessionImpl;
            _rs = rs;
            _maxRows = maxRows;
            _criteriaLoader = criteriaLoader;
          
        }

        public object Get()
        {
            return _criteriaLoader.ExternalReadRow(_session, _rs, false);
        }

        public T Get<T>()
        {
            return (T) Get();
        }

        public void Close()
        {
            if (_opened)
            {
                _externalsessionImpl.Batcher.CloseReader(_rs);
                _opened = false;
            }
        }

        public bool Next()
        {
            
            _position++;
            if (_position>_maxRows)
                Close();
            if (_opened)
            {
                bool _eof = !_rs.Read();
                if (_eof) Close();
            }
            return _opened;
        }

        public bool Scroll(int countNext)
        {
            bool resut = true;
            while (countNext>0)
            {
                countNext--;
               resut  = resut&& Next();
            }
            return resut;
        }

        void IDisposable.Dispose()
        {
           Close();
        }
    }
}