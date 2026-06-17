import System.Collections.Generic as C

def test_contains():
    l = C.List[int]()
    l.Add(42)
    assert 42 in l
    assert 43 not in l

def test_dict_items():
    d = C.Dictionary[int, str]()
    d[42] = "a"
    # QuantConnect fork: the collections.abc Mapping mixin is not applied to
    # .NET dictionaries, so .items() is not provided; use the .NET API instead.
    assert not hasattr(d, "items")
    assert d.Count == 1
    assert list(d.Keys) == [42]
    assert d[42] == "a"
