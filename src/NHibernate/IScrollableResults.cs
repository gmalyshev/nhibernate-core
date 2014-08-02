using System;

namespace NHibernate
{
    /// <summary>
    /// A result iterator that allows moving around within the results by arbitrary increments. 
    /// Possible implementation
    /// </summary>
    public interface IScrollableResults:IDisposable
    {
        /// <summary>
        ///  Get Current object
        /// </summary>
        /// <returns></returns>
        object Get();
        
        /// <summary>
        /// Get current object (with cast)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T Get<T>();

        /// <summary>
        /// Close dataset
        /// </summary>
        void Close();

        /// <summary>
        /// Featch Next Row
        /// </summary>
        /// <returns></returns>
        bool Next();

        /// <summary>
        /// Featch next countNext Row
        /// </summary>
        /// <param name="countNext"></param>
        /// <returns></returns>
        bool Scroll(int countNext);
    }
}