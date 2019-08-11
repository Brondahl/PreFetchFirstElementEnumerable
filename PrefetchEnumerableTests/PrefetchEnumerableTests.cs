using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using PreFetchFirstElementEnumerable;

namespace PrefetchEnumerableTests
{
  [TestFixture]
  public class PrefetchEnumerableTests
  {
    private static int _iteratorPosition = -1;
    private static int _executeFirstStep = 0;
    private static int _executeSecondStep = 0;

    #region TestIterations
    private static IEnumerable<int> PositionTrackedFiniteIterator()
    {
      _executeFirstStep++;
      _iteratorPosition = 0;
      yield return (_iteratorPosition + 1) * 10;

      _executeSecondStep++;
      _iteratorPosition = 1;
      yield return (_iteratorPosition + 1) * 10;

      _iteratorPosition = 2;
      yield return (_iteratorPosition + 1) * 10;

      _iteratorPosition = 3;
      yield return (_iteratorPosition + 1) * 10;

      _iteratorPosition = -2;
    }

    private static IEnumerable<int> PositionTrackedInfiniteIterator()
    {
      _executeFirstStep++;
      _iteratorPosition = 0;
      while (true)
      {
        yield return (_iteratorPosition + 1) * 10;

        if (_iteratorPosition == 0)
        {
          _executeSecondStep++;
        }

        if (_iteratorPosition == int.MaxValue)
        {
          _iteratorPosition = 0;
        }
        else
        {
          _iteratorPosition++;
        }
      }
    }

    private class TrackingIEnumerable : IEnumerable<int>
    {
      private class Enumerator : IEnumerator<int>
      {
        private static bool _disposed = false;
        public void Dispose() { _disposed = true; }

        public bool MoveNext()
        {
          if (_disposed)
          {
            Reset();
            _disposed = false;
          }
          switch (_iteratorPosition)
          {
            case -1:
              _executeFirstStep++;
              _iteratorPosition = 0;
              return true;
            case 0:
              _executeSecondStep++;
              _iteratorPosition = 1;
              return true;
            case 1:
            case 2:
              _iteratorPosition++;
              return true;
            case 3:
              _iteratorPosition = -2;
              return false;
            default:
              throw new NotImplementedException();
          }
        }

        public void Reset()
        {
          _iteratorPosition = -1;
        }

        public int Current => (_iteratorPosition + 1) * 10;

        object IEnumerator.Current => Current;
      }

      public IEnumerator<int> GetEnumerator() => new Enumerator();

      IEnumerator IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }
    }

    private static IEnumerable<int> SelectUsingValuesToTrackState()
    {
      return new[] { 10, 20, 30, 40, 50 }.Select(val =>
      {
        _iteratorPosition++;
        if (val == 10) { _iteratorPosition = 0; _executeFirstStep++; }
        if (val == 20) { _executeSecondStep++; }
        if (val == 50) { _iteratorPosition = -2; }
        return val;
      }).Where(val => val > 5 && val < 45);
    }

    private static IEnumerable<int> SelectTrackingAnInternalStateVariable()
    {
      var internalState = -1;
      return new[] { 0, 10, 20, 30, 40, 50 }.Select(val =>
      {
        if (val == 0)
        {
          _iteratorPosition = -1;
          internalState = 0;
        }
        else
        {
          _iteratorPosition++;
          internalState++;
        }

        if (internalState == 1) { _executeFirstStep++; }
        if (internalState == 2) { _executeSecondStep++; }

        if (internalState == 5)
        {
          _iteratorPosition = -2;
          internalState = 0;
        }
        return val;
      }).Where(val => val > 5 && val < 45);
    }

    public static TestCaseData[] FiniteTrackedIteratorFactories =>
        new TestCaseData[]
        {
                new TestCaseData((Func<IEnumerable<int>>)(PositionTrackedFiniteIterator)).SetName("Finite Iterator"),
                new TestCaseData((Func<IEnumerable<int>>)(() => new TrackingIEnumerable())).SetName("Manual Enumerator"),
                new TestCaseData((Func<IEnumerable<int>>)(SelectUsingValuesToTrackState)).SetName("value-based Select"),
                new TestCaseData((Func<IEnumerable<int>>)(SelectTrackingAnInternalStateVariable)).SetName("state-based Select"),
        };

    public static TestCaseData[] InfiniteTrackedIteratorFactories =>
        new TestCaseData[]
        {
                new TestCaseData((Func<IEnumerable<int>>)(PositionTrackedInfiniteIterator)).SetName("Infinite Iterator"),
        };

    public static TestCaseData[] AllTrackedIteratorFactories =>
        FiniteTrackedIteratorFactories.Concat(InfiniteTrackedIteratorFactories).ToArray();

    public static TestCaseData[] PrefetchPairings(TestCaseData[] cases)
    {
      return cases.Select(iterFactData =>
      {
        var untypedIterFact = iterFactData.Arguments.Single();
        var originalName = iterFactData.TestName;
        var iterFact = (Func<IEnumerable<int>>)untypedIterFact;
        Func<IEnumerable<int>> prefetchIterFact = () => new PreFetchFirstElementEnumerable<int>(iterFact());
        var testCaseData = new TestCaseData(prefetchIterFact).SetName("Prefetch " + originalName);
        return testCaseData;
      }).ToArray();
    }

    public static TestCaseData[] AllTrackedIteratorFactories_IncludingPrefetchedPairs =>
        AllTrackedIteratorFactories.Concat(PrefetchPairings(AllTrackedIteratorFactories)).ToArray();

    public static TestCaseData[] FiniteTrackedIteratorFactories_IncludingPrefetchedPairs =>
        FiniteTrackedIteratorFactories.Concat(PrefetchPairings(FiniteTrackedIteratorFactories)).ToArray();
    #endregion

    [SetUp]
    public void ResetState()
    {
      _iteratorPosition = -1;
      _executeFirstStep = 0;
      _executeSecondStep = 0;
    }

    #region Invoke
    [Test,
    TestCaseSource(nameof(AllTrackedIteratorFactories))]
    public void _NonPrefetch_InvokingIteratorDoesNotChangePosition(Func<IEnumerable<int>> trackedIterationFactory)
    {
      _iteratorPosition.Should().Be(-1);
      var iter = trackedIterationFactory();
      _iteratorPosition.Should().Be(-1);
      _executeFirstStep.Should().Be(0);
      _executeSecondStep.Should().Be(0);
    }

    [Test,
     TestCaseSource(nameof(AllTrackedIteratorFactories))]
    public void _Prefetch_InvokingAPrefetchedIteratorDoesChangePositionAndDoesExecuteFirstStep(Func<IEnumerable<int>> trackedIterationFactory)
    {
      _iteratorPosition.Should().Be(-1);
      var iter = new PreFetchFirstElementEnumerable<int>(trackedIterationFactory());
      _iteratorPosition.Should().Be(0);
      _executeFirstStep.Should().Be(1);
      _executeSecondStep.Should().Be(0);
    }
    #endregion

    #region Move Once
    [Test,
     TestCaseSource(nameof(AllTrackedIteratorFactories_IncludingPrefetchedPairs))]
    public void _Both_MovingIteratorOnceWithLinqDoesChangePositionButDoesNotExecuteSecondCodeStep(Func<IEnumerable<int>> trackedIterationFactory)
    {
      var iter = trackedIterationFactory();
      iter.Take(1).Single().Should().Be(10);
      _iteratorPosition.Should().Be(0);
      _executeFirstStep.Should().Be(1);
      _executeSecondStep.Should().Be(0);
    }
    #endregion

    #region Move Once Repeatedly
    [Test,
     TestCaseSource(nameof(AllTrackedIteratorFactories_IncludingPrefetchedPairs))]
    public void _Both_MovingIteratorOnceWithLinqRepeatedlyExecutesCodeRepeatedly(Func<IEnumerable<int>> trackedIterationFactory)
    {
      var iter = trackedIterationFactory();
      iter.Take(1).Single().Should().Be(10);
      iter.Take(1).Single().Should().Be(10);
      iter.Take(1).Single().Should().Be(10);
      _iteratorPosition.Should().Be(0);
      _executeFirstStep.Should().Be(3);
      _executeSecondStep.Should().Be(0);
    }
    #endregion

    #region Move Twice 
    [Test,
     TestCaseSource(nameof(AllTrackedIteratorFactories_IncludingPrefetchedPairs))]
    public void _Both_MovingIteratorTwiceWithLinqExecutesSecondCodeStep(Func<IEnumerable<int>> trackedIterationFactory)
    {
      var iter = trackedIterationFactory();
      iter.Take(2).Should().BeEquivalentTo(10, 20);
      _iteratorPosition.Should().Be(1);
      _executeFirstStep.Should().Be(1);
      _executeSecondStep.Should().Be(1);
    }
    #endregion

    #region Move Thrice 
    [Test,
     TestCaseSource(nameof(AllTrackedIteratorFactories_IncludingPrefetchedPairs))]
    public void _Both_MovingIteratorThriceWithLinqDoesChangePositionButExecutesCodeOnce(Func<IEnumerable<int>> trackedIterationFactory)
    {
      var iter = trackedIterationFactory();
      iter.Take(3).Should().BeEquivalentTo(10, 20, 30);
      _iteratorPosition.Should().Be(2);
      _executeFirstStep.Should().Be(1);
      _executeSecondStep.Should().Be(1);
    }
    #endregion

    #region Use Enumerator directly
    [Test,
     TestCaseSource(nameof(FiniteTrackedIteratorFactories))]
    public void _NonPrefetch_MovingIteratorWithEnumeratorBehavesCorrectly(Func<IEnumerable<int>> trackedIterationFactory)
    {
      var iter = trackedIterationFactory();    StateShouldBe(-1, 0, 0);
      var itEnum = iter.GetEnumerator();       StateShouldBe(-1, 0, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(0, 1, 0);
      itEnum.Current.Should().Be(10);          StateShouldBe(0, 1, 0);
      itEnum.Current.Should().Be(10);          StateShouldBe(0, 1, 0);
      itEnum.Current.Should().Be(10);          StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(1, 1, 1);
      itEnum.Current.Should().Be(20);          StateShouldBe(1, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(2, 1, 1);
      itEnum.Current.Should().Be(30);          StateShouldBe(2, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(3, 1, 1);
      itEnum.MoveNext().Should().BeFalse();    StateShouldBe(-2, 1, 1);

      ResetState();

      itEnum = iter.GetEnumerator();           StateShouldBe(-1, 0, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(1, 1, 1);
      itEnum.Current.Should().Be(20);          StateShouldBe(1, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(2, 1, 1);
      itEnum.Current.Should().Be(30);          StateShouldBe(2, 1, 1);

      ResetState();

      iter = trackedIterationFactory();        StateShouldBe(-1, 0, 0);
      itEnum = iter.GetEnumerator();           StateShouldBe(-1, 0, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(1, 1, 1);
      itEnum.Current.Should().Be(20);          StateShouldBe(1, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(2, 1, 1);
      itEnum.Current.Should().Be(30);          StateShouldBe(2, 1, 1);
    }

    [Test,
     TestCaseSource(nameof(FiniteTrackedIteratorFactories))]
    public void _Prefetch_MovingIteratorWithEnumeratorBehavesCorrectly(Func<IEnumerable<int>> trackedIterationFactory)
    {
      var iter = new PreFetchFirstElementEnumerable<int>(trackedIterationFactory()); StateShouldBe(0, 1, 0);
      var itEnum = iter.GetEnumerator();       StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(0, 1, 0);
      itEnum.Current.Should().Be(10);          StateShouldBe(0, 1, 0);
      itEnum.Current.Should().Be(10);          StateShouldBe(0, 1, 0);
      itEnum.Current.Should().Be(10);          StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(1, 1, 1);
      itEnum.Current.Should().Be(20);          StateShouldBe(1, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(2, 1, 1);
      itEnum.Current.Should().Be(30);          StateShouldBe(2, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(3, 1, 1);
      itEnum.MoveNext().Should().BeFalse();    StateShouldBe(-2, 1, 1);

      ResetState();

      itEnum = iter.GetEnumerator();           StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(1, 1, 1);
      itEnum.Current.Should().Be(20);          StateShouldBe(1, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(2, 1, 1);
      itEnum.Current.Should().Be(30);          StateShouldBe(2, 1, 1);

      ResetState();

      iter = new PreFetchFirstElementEnumerable<int>(trackedIterationFactory()); StateShouldBe(0, 1, 0);
      itEnum = iter.GetEnumerator();           StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(0, 1, 0);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(1, 1, 1);
      itEnum.Current.Should().Be(20);          StateShouldBe(1, 1, 1);
      itEnum.MoveNext().Should().BeTrue();     StateShouldBe(2, 1, 1);
      itEnum.Current.Should().Be(30);          StateShouldBe(2, 1, 1);

    }

    private void StateShouldBe(int pos, int first, int second)
    {
      _iteratorPosition.Should().Be(pos);
      _executeFirstStep.Should().Be(first);
      _executeSecondStep.Should().Be(second);
    }

    #endregion

    #region Read all
    [Test,
     TestCaseSource(nameof(FiniteTrackedIteratorFactories_IncludingPrefetchedPairs))]
    public void _Both_CompletingIteratorSetsFinalPosition(Func<IEnumerable<int>> trackedIterationFactory)
    {
      var iter = trackedIterationFactory().ToList();
      iter.First().Should().Be(10);
      iter.Last().Should().Be(40);
      _iteratorPosition.Should().Be(-2);
      _executeFirstStep.Should().Be(1);
      _executeSecondStep.Should().Be(1);
    }
    #endregion
  }
}
