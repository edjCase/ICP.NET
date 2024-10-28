# Candid - Library of Candid Encoding, Models and Helpers

- Nuget: [`EdjCase.ICP.Candid`](https://www.nuget.org/packages/EdjCase.ICP.Candid)

# Using Native Candid Types

- `CandidArg` is the set of candid typed value parameters that is sent to a IC canister for a request
- `CandidTypedValue` is the combo of a `CandidValue` and its corresponding `CandidType`
- `CandidValue` is a raw candid value
- `CandidType` is the type definition of a candid type

## Create Type

Some Examples:

```cs
// Nat (positive integer)
CandidType natType = CandidType.Nat(); // shorthand for `new CandidPrimitiveType(PrimitiveType.Nat)`

// Text (string)
CandidType textType = CandidType.Text(); // shorthand for `new CandidPrimitiveType(PrimitiveType.Text)`

// Opt (optional/nullable) Principal (identifier)
CandidType optionalPrincipalType = CandidType.Opt(CandidType.Principal());

// Record (dictionary/object)
CandidType recordType = new CandidRecordType(new Dictionary<CandidTag, CandidType>
{
    { CandidTag.FromName("name"), CandidType.Text() },
    { CandidTag.FromName("age"), CandidType.Nat() }
});

// Variant (union)
CandidType variantType = new CandidVariantType(new Dictionary<CandidTag, CandidType>
{
    { CandidTag.FromName("option1"), CandidType.Bool() },
    { CandidTag.FromName("option2"), CandidType.Null() }
});

// Vec (list/array) of Float32
CandidType float32Vec = new CandidVectorType(CandidType.Float32());
```

## Create Value

Some Examples:

```cs
// Nat (positive integer)
CandidValue natType = CandidValue.Nat(4);

// Text (string)
CandidValue textType = CandidValue.Text("SomeText");

// Opt (optional/nullable) Principal (identifier)
CandidValue optionalPrincipalType = new CandidOptional(CandidValue.Principal(Principal.Anonymous()));

// Record (dictionary/object)
CandidValue recordType = new CandidRecord(new Dictionary<CandidTag, CandidValue>
{
    { CandidTag.FromName("name"), CandidValue.Text("Name1") },
    { CandidTag.FromName("age"), CandidValue.Nat(21) }
});

// Variant (union)
CandidValue variantType = new CandidVariant("option1", CandidValue.Bool(true));

// Vec (list/array) of Float32
CandidValue float32Vec = new CandidVector(new CandidValue[]
{
    CandidValue.Float32(1.1f),
    CandidValue.Float32(2.2f),
    CandidValue.Float32(3.3f)
});
```

## Parse From bytes

When candid is encoded, its in the form of a `CandidArg`. Individual values and types are not usually encoded

```cs
CandidArg arg = CandidByteParser.Parse(rawCandidBytes);
```

OR

```cs
CandidArg arg = CandidArg.FromBytes(rawCandidBytes);
```

## Reading Values (without custom types)

```cs
// Assume arg is `(Nat, record { title : Text; length : Nat; })`
CandidArg arg = ...;
CandidTypedValue firstTypedValue = arg.Values[0];
UnboundedUInt natValue = firstTypedValue.Value.AsNat();
CandidTypedValue secondTypedValue = arg.Values[1];
CandidRecord recordValue = secondTypedValue.Value.AsRecord();
string title = recordValue["title"].AsText();
UnboundedUInt length = recordValue["length"].AsNat();
```

# Using Self Defined Types

Self defined types are helpful for predefining classes to the candid schema of an endpoint. This can be done manually (as shown below) or done automattically with the [ClientGenerator](../ClientGenerator/README.md)

## From Candid To Self Defined Type

Single argument:

```cs
CandidArg arg = ...;
MyObj1 obj = arg.ToObjects<MyObj1>();
```

Multi arguments:

```cs
CandidArg arg = ...;
(MyObj1 obj, MyObj2 obj2) = arg.ToObjects<MyObj1, MyObj2>();
```

## To Candid From Self Defined Type

```cs
// Serialze
MyObj obj = new MyObj
{
    Title = "Title 1",
    IsGoodTitle = false
};
CandidTypedValue value = CandidTypedValue.FromObject(obj);
```

## Defining Variant Types

```cs
[Variant] // Required to flag as variant
public class MyVariant
{
	[VariantTagProperty] // Flag for tag/enum property, not required if name is `Tag`
	public MyVariantTag Tag { get; set; }
	[VariantValueProperty] // Flag for value property, not required if name is `Value`
	public object? Value { get; set; }


	// This method is used to specify if the option has a type/value associated
	[VariantOption("o2")] // Specify the candid tag if different than 'As{CandidTag}' like 'Option2' here
	public string AsOption2()
	{
		return (string)this.Value!;
	}
}

public enum MyVariantTag
{
    [CandidName("o1")] // Used to override name for candid
    Option1,
    [CandidName("o2")]
    Option2
}
```

Or if variant options have no type, just an Enum can be used

```cs
public enum MyVariant
{
    [CandidName("o1")]
    Option1,
    [CandidName("o2")]
    Option2
}
```

## Defining Record Types

```cs
public class MyRecord
{
    [CandidName("title")] // Used to override name for candid
    public string Title { get; set; }
    [CandidName("is_good_title")]
    public bool IsGoodTitle { get; set; }
}
```

### NOTE: [CandidName(...)] is not needed if the property name is exactly the same as the candid name

```cs
// Equivalent to above
public class MyRecord
{
    public string title { get; set; }
    public bool is_good_title { get; set; }
}
```

## Raw Candid Types

If you want to use the raw candid forms for the value, then the candid type definition must be specified with the CandidTypeDefinitionAttribute
The definition is in text form of candid (like in a \*.did file) as seen here https://github.com/dfinity/candid/blob/master/spec/Candid.md#core-grammar
The text form is used because attributes cannot have an object type, so cannot accept the CandidType class

NOTE: func and service do not have a compatible type that is not the raw candid form, so they must have the CandidTypeDefinitionAttribute

```
public class MyRecord
{
    [CandidTypeDefinition("() -> ()")]
    public CandidFunc Callback { get; set; }
}
```

## Defining Other Types

```cs
(C# type) -> (Candid type)

UnboundedUInt -> Nat
byte -> Nat8
ushort -> Nat16
uint -> Nat32
ulong -> Nat64
UnboundedInt -> Int
sbyte -> Int8
short -> Int16
int -> Int32
long -> Int64
string -> Text
float -> Float32
double -> Float64
bool -> Bool
Principal -> Principal
List<T> -> Vec T
T[] -> Vec T
OptionalValue<T> -> Opt T
EmptyValue -> Empty
ReservedValue -> Reserved
NullValue -> Null
* CandidFunc -> Func
* CandidService -> Service

* Raw types require the [CandidTypeDefinition("...")] attribute
```
