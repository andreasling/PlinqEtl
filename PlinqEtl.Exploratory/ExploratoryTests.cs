using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;

namespace PlinqEtl.Exploratory
{
	[TestFixture]
	public class ExploratoryTests
	{
		[Test]
		public void ShouldCatchException()
		{
			var actual =
				(from i in Enumerable.Range(0,2)
				 select ThrowOnOdds(i));

			Assert.That(actual.Single(), Is.EqualTo(0));
			Assert.That(actual.GetExceptions().Single().Message, Is.EqualTo("error: 1"));
		}

		public int ThrowOnOdds(int i)
		{
			if (i % 2 == 0)
				return i;

			throw new Exception("error: " + i);
		}
	}

	public static class PlinqEtlExtensions
	{
		public static IEnumerable<TResult> Select<TSource, TResult> (this IEnumerable<TSource> source, Func<TSource, TResult> selector)
		{
			/* foreach (var item in source) {

				TResult result;
				try {
					result = selector(item);
				} catch (Exception ex) {
					continue;
				}

				yield return result;
			} */
			return new CatchingEnumerator<TSource, TResult>(source, selector);
		}

		public static IEnumerable<Exception> GetExceptions<TSource> (this IEnumerable<TSource> source)
		{
			if (source is ICatchingEnumerator)
				return (source as ICatchingEnumerator).Exceptions;

			return Enumerable.Empty<Exception>();
		}

		interface ICatchingEnumerator
		{
			IEnumerable<Exception> Exceptions { get; }
		}

		class CatchingEnumerator<TSource, TResult> : ICatchingEnumerator, IEnumerable<TResult>, IEnumerator<TResult>
		{
			private IEnumerable<TSource> source;
			private Func<TSource, TResult> selector;
			private IList<Exception> exceptions = new List<Exception>();
			public IEnumerable<Exception> Exceptions { get { return exceptions; } }

			IEnumerator<TSource> sourceEnumerator {
				get;
				set;
			}

			public CatchingEnumerator(IEnumerable<TSource> source, Func<TSource, TResult> selector)
			{
				this.source = source;
				this.selector = selector;
			}


			#region IEnumerable implementation
			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
			{
				this.sourceEnumerator = source.GetEnumerator();
				return this;
			}
			#endregion

			#region IEnumerable implementation
			IEnumerator<TResult> IEnumerable<TResult>.GetEnumerator ()
			{
				this.sourceEnumerator = source.GetEnumerator();
				return this;
			}
			#endregion

			#region IDisposable implementation
			void IDisposable.Dispose ()
			{
				this.sourceEnumerator.Dispose();
			}
			#endregion

			private TResult currentResult = default(TResult);
			#region IEnumerator implementation
			bool System.Collections.IEnumerator.MoveNext ()
			{
				while (this.sourceEnumerator.MoveNext ()) {
					try {
						currentResult = selector(this.sourceEnumerator.Current);
						return true;
					} catch (Exception ex) {
						exceptions.Add(ex);
						continue;
					}
				}

				return false;
			}

			void System.Collections.IEnumerator.Reset ()
			{
				this.sourceEnumerator.Reset();
			}

			object System.Collections.IEnumerator.Current {
				get {
					return currentResult;
				}
			}
			#endregion

			#region IEnumerator implementation
			TResult IEnumerator<TResult>.Current {
				get {
					return currentResult;
				}
			}
			#endregion
		}
	}
}

