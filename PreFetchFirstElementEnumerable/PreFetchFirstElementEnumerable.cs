using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PreFetchFirstElementEnumerable
{
  public static class ExtensionHelper
  {
    /// <summary>
    /// In many cases, your IEnumerable needs to do a bunch of work before it can
    /// return the first value, and it doesn't do any of that work until the first
    /// is going to be actually used.
    /// If you want to force that work to happen immediately, then the easiest way
    /// is to call .ToList(), but that will reify the whole collection immediately.
    /// The purpose of this method is to force the start of the execution to happen
    /// immediately but to retain the lazy loading (and particularly the streaming!)
    /// of the rest of the IEnumerable.
    ///
    /// We achieve that by immediately fetching the first value, and then when the
    /// consumer of the IEnumerable asks for the data, first returning that
    /// pre-fetched element and then leaving the rest of the enumeration to resolve
    /// as usual.
    /// </summary>
    public static IEnumerable<T> InitiatePrefetchOfFirstElement<T>(this IEnumerable<T> source) => new PreFetchFirstElementEnumerable<T>(source);
    /// <summary>
    /// In many cases, your IEnumerable needs to do a bunch of work before it can
    /// return the first value, and it doesn't do any of that work until the first
    /// is going to be actually used.
    /// If you want to force that work to happen immediately, then the easiest way
    /// is to call .ToList(), but that will reify the whole collection immediately.
    /// The purpose of this method is to force the start of the execution to happen
    /// immediately but to retain the lazy loading (and particularly the streaming!)
    /// of the rest of the IEnumerable.
    ///
    /// We achieve that by immediately fetching the first value, and then when the
    /// consumer of the IEnumerable asks for the data, first returning that
    /// pre-fetched element and then leaving the rest of the enumeration to resolve
    /// as usual.
    /// </summary>
    public static IEnumerable<T> InitiatePrefetchOfFirstElement<T>(this IEnumerable source) => new PreFetchFirstElementEnumerable<T>(source.Cast<T>());
    /// <summary>
    /// In many cases, your IEnumerable needs to do a bunch of work before it can
    /// return the first value, and it doesn't do any of that work until the first
    /// is going to be actually used.
    /// If you want to force that work to happen immediately, then the easiest way
    /// is to call .ToList(), but that will reify the whole collection immediately.
    /// The purpose of this method is to force the start of the execution to happen
    /// immediately but to retain the lazy loading (and particularly the streaming!)
    /// of the rest of the IEnumerable.
    ///
    /// We achieve that by immediately fetching the first value, and then when the
    /// consumer of the IEnumerable asks for the data, first returning that
    /// pre-fetched element and then leaving the rest of the enumeration to resolve
    /// as usual.
    /// </summary>
    public static IEnumerable<object> InitiatePrefetchOfFirstElement(this IEnumerable source) => source.InitiatePrefetchOfFirstElement<object>();
  }

  /// <summary>
  /// In many cases, your IEnumerable needs to do a bunch of work before it can
  /// return the first value, and it doesn't do any of that work until the first
  /// is going to be actually used.
  /// If you want to force that work to happen immediately, then the easiest way
  /// is to call .ToList(), but that will reify the whole collection immediately.
  /// The purpose of this class is to force the start of the execution to happen
  /// immediately but to retain the lazy loading (and particularly the streaming!)
  /// of the rest of the IEnumerable.
  ///
  /// We achieve that by immediately fetching the first value, and then when the
  /// consumer of the IEnumerable asks for the data, first returning that
  /// pre-fetched element and then leaving the rest of the enumeration to resolve
  /// as usual.
  /// </summary>
  public class PreFetchFirstElementEnumerable<T> : IEnumerable<T>
  {
    private readonly IEnumerator<T> _enumerator;

    public PreFetchFirstElementEnumerable(IEnumerable<T> source) : this(source.GetEnumerator()) {}
    public PreFetchFirstElementEnumerable(IEnumerator<T> sourceEnumerator)
    {
      _enumerator = new PreFetchFirstElementEnumerator<T>(sourceEnumerator);
    }

    public IEnumerator<T> GetEnumerator() => _enumerator;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }

  /// <summary>
  /// Responsible for actually doing the pre-fetching and managing the
  /// state around returning the first element correctly.
  /// </summary>
  /// <remarks>
  /// Note that correct invocation of the IEnumerator is:
  ///     myEnumerator.MoveNext()
  ///     myEnumerator.Current        //return 0th element (pre-fetched)
  ///     myEnumerator.MoveNext()
  ///     myEnumerator.Current        //return 1st element (real)
  ///     myEnumerator.MoveNext()
  ///     ... etc
  /// So we need to track the state of the first *two* MoveNext invocations,
  /// so that we know whether to return the pre-fetched value, or the real next value.
  /// </remarks>
  public class PreFetchFirstElementEnumerator<T> : IEnumerator<T>
  {
    private readonly IEnumerator<T> _enumerator;
    private bool preFetchedMoveNextResult;
    private T preFetchedElement;
    private bool hasCalledMoveNextOnce;
    private bool hasCalledMoveNextAgain;

    public PreFetchFirstElementEnumerator(IEnumerator<T> source)
    {
      _enumerator = source;
      PreFetchElement();
    }

    private void PreFetchElement()
    {
      preFetchedMoveNextResult = _enumerator.MoveNext();
      preFetchedElement = _enumerator.Current;
      hasCalledMoveNextOnce = false;
      hasCalledMoveNextAgain = false;
    }

    public bool MoveNext()
    {
      //A more natural way to express this method might have been:
      //
      //    if(!hasCalledMoveNextOnce)
      //    {
      //       hasCalledMoveNextOnce = true;
      //       return preFetchedMoveNextResult;
      //    }
      //    
      //    if(!hasCalledMoveNextAgain)
      //    {
      //       hasCalledMoveNextAgain = true; 
      //    }
      //    
      //    return _enumerator.MoveNext();
      //
      // But if this is used in performance sensitive code then we want to
      // ensure we're optimising the steady-state case.
      //
      // Hence structuring the code as follows, which is logically equivalent,
      // but requires only one bool-check in the steady-state case.

      if (hasCalledMoveNextAgain)
      {
        return _enumerator.MoveNext();
      }

      if (hasCalledMoveNextOnce)
      {
        hasCalledMoveNextAgain = true;
        return _enumerator.MoveNext();
      }

      hasCalledMoveNextOnce = true;
      return preFetchedMoveNextResult;
    }

    public T Current
    {
      // As in MoveNext() we're optimising for detecting the steady-state case
      // as cheaply as possible.
      // If we wished to perfectly re-create a normal IEnumerator<T>, then once
      // we've established that we're not in the steady-state case,, then we
      // would have something like this:
      //       if(hasCalledMoveNextOnce) { return prefetch; } else { throw SomeException; }
      // But from the MSDN IEnumerator<> spec:
      //    "You must call the MoveNext method to advance the enumerator to the first element of the collection before reading the value of Current; otherwise, Current is undefined."
      // Since it's undefined. Returning the pre-fetched element is a legal implementation.
      get
      {
        if (hasCalledMoveNextAgain)
        {
          return _enumerator.Current;
        }
        return preFetchedElement;
      }
    }

    public void Reset()
    {
      _enumerator.Reset();
      PreFetchElement();
    }

    public void Dispose() => _enumerator.Dispose();
    object IEnumerator.Current => this.Current;
  }
}
