PreFetchFirstElementEnumerable
================

Library that provides a wrapper around an existing `IEnumerable<T>` which immediately pre-fetches the first element of the source.
This means that any up-front initiation code is executed immediately, rather than when later iterated over, but retains the lazy streaming nature of the `IEnumerable<T>` for all later elements.

An example practical use-case could be initiating the SQL Query backing an EntityFramework streamed query.

Version History
----------------------------------------------

 * v1.0.0 Added Documentation. Package is Live.
 * v0.9.9 Fixed issue with re-using the IEnumerable and Unit Tests.
 * v0.9.1 Forgot to update release notes.
 * v0.9.0 Initial Code with no Docs or Tests
 * v0.0.1 Blank project to wire up infrastructure

Example Usage
-------------

```
public void Example(IEnumberable<int> inputEnumerable)
{
  var prefetchedEnumerable = new PreFetchFirstElementEnumerable<int>(inputEnumerable);
  var alternatePrefetchEnumerable = inputEnumerable.InitiatePrefetchOfFirstElement();
  var anotherEnumerableWithExplicitTyping = inputEnumerable.InitiatePrefetchOfFirstElement<int>();
}

public void UntypedExample(IEnumberable inputUntypedEnumerable)
{
  IEnumerable<object> prefetchedEnumerable = new PreFetchFirstElementEnumerable<object>(inputEnumerable.Cast<object>);
  IEnumerable<object> aDifferentEnumerable = inputEnumerable.InitiatePrefetchOfFirstElement();
}
```
