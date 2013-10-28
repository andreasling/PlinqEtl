using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PlinqEtl.Core
{
    public static class PlinqEtlExtensions
    {
        public static CatchingIterator<TSource> ToCatching<TSource>(this IEnumerable<TSource> source)
        {
            return new CatchingIterator<TSource>(source);
        }

        public static CatchingSelectIterator<TSource, TResult> Select<TSource, TResult>(this CatchingIterator<TSource> source,
                                                                    Func<TSource, TResult> selector)
        {
            return source.Select(selector);
        }

        public static IEnumerable<Exception> GetExceptions<TSource>(this IEnumerable<TSource> source)
        {
            if (source is ICatchingIterator)
                return (source as ICatchingIterator).Exceptions;

            throw new InvalidOperationException("not a catching enumerable");
        }

        public interface ICatchingIterator
        {
            IEnumerable<Exception> Exceptions { get; }
        }

        public class CatchingIterator<TSource> : IEnumerable<TSource>, IEnumerator<TSource>, ICatchingIterator
        {
            protected readonly IEnumerable<TSource> source;
            protected IEnumerator<TSource> sourceEnumerator;
            protected readonly IList<Exception> exceptions = new List<Exception>();

            public CatchingIterator(IEnumerable<TSource> source)
            {
                this.source = source;
            }

            public IEnumerator<TSource> GetEnumerator()
            {
                sourceEnumerator = source.GetEnumerator();
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Dispose()
            {
                sourceEnumerator.Dispose();
            }

            public bool MoveNext()
            {
                return sourceEnumerator.MoveNext();
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public TSource Current
            {
                get { return sourceEnumerator.Current; }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public CatchingSelectIterator<TSource, TResult> Select<TResult>(Func<TSource, TResult> selector)
            {
                return new CatchingSelectIterator<TSource, TResult>(this, selector);
            }

            public IEnumerable<Exception> Exceptions
            {
                get
                {
                    return exceptions;
                }
            }
        }
        public class CatchingSelectIterator<TSource, TResult> : IEnumerable<TResult>, IEnumerator<TResult>, ICatchingIterator
        {
            private readonly CatchingIterator<TSource> source;
            private readonly ICatchingIterator parent;
            private IEnumerator<TSource> sourceEnumerator;
            private readonly IList<Exception> exceptions = new List<Exception>();
            private readonly Func<TSource, TResult> selector;

            public CatchingSelectIterator(CatchingIterator<TSource> source, Func<TSource, TResult> selector)
            {
                this.source = source;
                this.selector = selector;
            }

            public IEnumerator<TResult> GetEnumerator()
            {
                sourceEnumerator = source.GetEnumerator();
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Dispose()
            {
                sourceEnumerator.Dispose();
            }

            public bool MoveNext()
            {
                while (sourceEnumerator.MoveNext())
                {
                    try
                    {
                        Current = selector(sourceEnumerator.Current);
                        return true;
                    }
                    catch (Exception exception)
                    {
                        exceptions.Add(exception);
                    }
                }

                return false;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public TResult Current { get; private set; }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public IEnumerable<TResult2> Select<TResult2>(Func<TResult, TResult2> selector)
            {
                return new CatchingSelectIterator<TSource, TResult2>(source, x => selector(this.selector(x)));
            }

            public IEnumerable<Exception> Exceptions
            {
                get { return source.Exceptions.Concat(exceptions); }
            }
        }
    }
}