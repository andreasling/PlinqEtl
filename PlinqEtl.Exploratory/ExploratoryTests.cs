using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using NUnit.Framework;
using System.Linq;
using PlinqEtl.Core;

namespace PlinqEtl.Exploratory
{
	[TestFixture]
	public class ExploratoryTests
    {
        [Test]
        public void ShouldEnumerateEmpty()
        {
            Assert.That(Enumerable.Empty<int>().ToCatching().ToArray(), Is.EqualTo(Enumerable.Empty<int>()));
        }

        [Test]
        public void ShouldEnumerateSingle()
        {
            Assert.That(Enumerable.Range(0, 1).ToCatching().ToArray(), Is.EqualTo(new[] {0}));
        }

        [Test]
        public void ShouldEnumerateMany()
        {
            Assert.That(Enumerable.Range(0, 3).ToCatching().ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void ShouldRepeatEnumerate()
        {
            var catching = Enumerable.Range(0, 1).ToCatching();
            Assert.That(catching.ToArray(), Is.EqualTo(new[] { 0 }));
            Assert.That(catching.ToArray(), Is.EqualTo(new[] { 0 }));
        }

        [Test]
		public void ShouldCatchSelectException()
		{
			var actual =
				from i in Enumerable.Range(0,2).ToCatching()
				select ThrowOnOdds(i);

            Assert.That(actual.Single(), Is.EqualTo(0));
			Assert.That(actual.GetExceptions().Single().Message, Is.EqualTo("1"));
		}

        [Test]
        public void ShouldCatchSourceException()
        {
            var actual =
                from i in Enumerable.Range(0, 2).ThrowWhere(i => i % 2 != 0).ToCatching()
                select i;

            Assert.That(actual.Single(), Is.EqualTo(0));
            Assert.That(actual.GetExceptions().Single().Message, Is.EqualTo("1"));
        }

	    [Test]
	    public void SelectIntoTest()
	    {
	        var actual =
	            from i in Enumerable.Range(0, 2).ToCatching()
	            select i + 1
	            into j
	            select j*2 into k
	            select k.ThrowIf(l => l == 4);

	        Assert.That(actual.Single(), Is.EqualTo(2));
            Assert.That(actual.GetExceptions().Single().Message, Is.EqualTo("4"));
	    }

        [Test]
        public void ShouldSelectFromDynamic()
        {
            dynamic row = new { id = 0 };
            var rows = new[] { row };
            var actual =
                from r in rows
                select new {r.id};
            
            Assert.That(actual.First().id, Is.EqualTo(0));
        }

        [Test]
        public void ShouldParseSeparatedToDynamic()
        {
            var reader = new StringReader("id\tvalue\n0\ta\n1\tb\n");

            var actual =
                from row in EnumerateRows(reader)
                select new { id = (int)row.id };

			var id = actual.First ().id;
            Assert.That(id, Is.EqualTo(0));
        }

	    private static IEnumerable<dynamic> EnumerateRows(StringReader reader)
	    {
	        string line = null;
	        Dictionary<string, int> columns = null;

            line = reader.ReadLine();

	        if (line != null)
	        {
                columns = line
	                .Split('\t')
	                .Select((column, index) => new
	                                               {
	                                                   column,
	                                                   index
	                                               })
	                .ToDictionary(ci => ci.column, ci => ci.index);
	            
	        }

	        line = reader.ReadLine();

	        while (line != null)
	        {
	            yield return new DynamicRow(columns, line);

	            line = reader.ReadLine();
	        }
	    }

	    public int ThrowOnOdds(int i)
		{
		    return i.ThrowIf(j => j%2 != 0);
		}
	}

	public class DynamicCell : DynamicObject
	{
		private readonly object value;

		public DynamicCell (object value)
		{
			this.value = value;
		}

		public override bool TryConvert (ConvertBinder binder, out object result)
		{
			//return base.TryConvert (binder, out result);
			if (binder.ReturnType == typeof(int)) {
				result = int.Parse(value as string);
			}
			else
				result = value;
			return true;
		}
	}

    public class DynamicRow : DynamicObject
    {
        private readonly Dictionary<string, int> columns;
        private readonly string[] cells;

        public DynamicRow(Dictionary<string, int> columns, string row)
        {
            this.columns = columns;
            cells = row.Split('\t');
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (columns.ContainsKey(binder.Name))
            {
                var index = columns[binder.Name];

                var s = cells[index];
                if (binder.ReturnType == typeof (int))
                    result = int.Parse(s);
                else
                    result = new DynamicCell(s);
                return true;
            }

            result = null;
            return false;
        }
    }

    public static class TestHelperExtensions
    {
        public static IEnumerable<TSource> ThrowWhere<TSource>(this IEnumerable<TSource> source, Predicate<TSource> predicate)
        {
            return source.Select(element => element.ThrowIf(predicate));
        }

        public static TSource ThrowIf<TSource>(this TSource element, Predicate<TSource> predicate)
        {
            if (predicate(element))
                throw new Exception(element.ToString());
            
            return element;
        }
    }

	/* public static class PlinqEtlExtensionsFirstTake
	{
		/ * public static IEnumerable<TResult> Select<TSource, TResult> (this IEnumerable<TSource> source, Func<TSource, TResult> selector)
		{
			/ * foreach (var item in source) {

				TResult result;
				try {
					result = selector(item);
				} catch (Exception ex) {
					continue;
				}

				yield return result;
			} * /
			return new CatchingEnumerator<TSource, TResult>(source, selector);
		} * /



		/ * public static IEnumerable<Exception> GetExceptions<TSource> (this IEnumerable<TSource> source)
		{
			if (source is ICatchingEnumerator)
				return (source as ICatchingEnumerator).Exceptions;

			return Enumerable.Empty<Exception>();
		} * /

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
	} */
}
