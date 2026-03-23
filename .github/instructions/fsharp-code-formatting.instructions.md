---
description: "F# code formatting and indentation conventions"
applyTo: "**/*.fs,**/*.fsx"
---

# FSharp Code Formatting Instructions

## Indentation and Delimiters

Block indentation should follow a general standard of 4 spaces per indentation level. Also, whatever is delimiting the scope (like `[` or `{` or `(`) should be aligned with the indentation level of the block when it is closed.
`match with` blocks, `let` bindings that span multiple lines, and function bodies should all follow this convention.

```fsharp
// Good: indentation by 4 spaces
for i in 1..10 do
    printfn $"{i}"


// unformatted 
let myList =
    [
        "first"
        "second"
        "third"
    ]


// Fantomas formatted
// cannot easily copy past additional 
// elements to the list
let myList = [ "first"; "second"; "third" ]

// Good: indentation by 4 spaces
// and aligned delimiters
let myList =
    [
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"
    ]


// Fantomas formatted
// cannot easily use the default indentation 
// setting of the editor, als moving the first 
// or last element to reorder is awkward
let myList =
    [ "first"
      "second"
      "third"
      "first"
      "second"
      "third"
      "first"
      "second"
      "third" ]


// Good: indentation by 4 spaces
// and aligned delimiters
type myRecord =
    {
        Name: string
        Age: float
        BirthDay: DateTime
    }


// Fantomas formatted
// Same problem, just quicly moving record 
// fields arround is not very easy
type myRecord =
    { Name: string
      Age: float
      BirthDay: DateTime }


// Good : indentation by 4 spaces
// and aligned delimiters
myList
|> List.append
    [
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"
        "first"
        "second"
        "third"        
    ]


// Fantomas formatted
myList
|> List.append
    [ "first"
      "second"
      "third"
      "first"
      "second"
      "third"
      "first"
      "second"
      "third" ]


// Good : indentation by 4 spaces
// and aligned delimiters
vars
|> List.sortBy(fun (_, xs) ->
    // some other code
    xs |> List.iter (printfn "%A")

    xs
    |> List.tail
    |> List.sumBy Variable.count
)


// Fantomas formatted
// again it is more difficult to see what is
// the indented block container (in this case ")")
// also separation betheen different subsections
// of the function body is lost
vars
|> List.sortBy (fun (_, xs) ->
    // some other code
    xs |> List.iter (printfn "%A")
    xs |> List.tail |> List.sumBy Variable.count)
```

## Declaration Order

In F#, declarations must appear in dependency order. A type or function must be declared **before** it is referenced.

- Always declare types and functions before using them
- Order matters within modules — read top to bottom
- Declare types in order **without** the `and` keyword whenever possible — only use `and` for mutually recursive types

```fsharp
// Bad - Converter references BookDto before it's declared
type SkipInvalidBooksConverter() =
    inherit JsonConverter<BookDto list>()

type BookDto = { Title: string }


// Good - BookDto declared first
type BookDto = { Title: string }

type SkipInvalidBooksConverter() =
    inherit JsonConverter<BookDto list>()


// Bad - using `and` when it's not necessary for recursion
type Container = { Items: Item list }
and Item = { Name: string }


// Good - separate declarations in dependency order
type Item = { Name: string }

type Container = { Items: Item list }
```

## Pipeline Formatting

Try to put pipeline operators at the beginning of the line when breaking long pipelines.

```fsharp
// Good: pipeline indentation
result
|> Result.map (fun x -> x + 1)
|> Result.toOption

// Fantomas formatted
result |> Result.map (fun x -> x + 1) |> Result.toOption
```

## Formatting Consistency

Avoid vanity formatting — formatting that is sensitive to the vertical alignment column and breaks when an identifier is renamed. Prefer standard indentation over aligning to a specific column position.

When consecutive lines apply the same pattern, they should be formatted the same way. If one long expression must be split across multiple lines, consider splitting sibling expressions the same way for visual consistency.

```fsharp
// Bad: vanity alignment — breaks when function name changes
let result  = calculate x
let outcome = transform y

// Good: standard indentation
let result = calculate x
let outcome = transform y
```

## Interface Definitions

Always use the `[<Interface>]` attribute on interfaces to prevent accidental conversion to abstract classes. The `IXxx` naming convention alone is not sufficient.

Exception: pure marker interfaces defined with `interface end` syntax don't require the attribute.

```fsharp
// Bad - missing attribute
type ILogger =
    abstract member Log: string -> unit

// Good - with attribute
[<Interface>]
type ILogger =
    abstract member Log: string -> unit

// Good - pure marker interface without attribute
type IMarker = interface end
```

## F# Idioms

### Unused Self Identifier

When a class member does not use the `this` identifier, discard it with an underscore:

```fsharp
// Bad - unused this identifier
type Counter() =
    member this.Increment() = 42

// Good - discarded with underscore
type Counter() =
    member _.Increment() = 42
```

### TryXxx Methods

When using BCL `TryXxx` methods that return `(bool * 'T)`, prefer pattern matching over checking the boolean result:

```fsharp
// Bad
let hasValue = dict.TryGetValue(key, &value)
if hasValue then
    // use value

// Good
match dict.TryGetValue(key) with
| true, value -> // use value
| false, _ -> // handle missing case

// Good - multiple conditions
match obj.TryGetProperty("foo"), obj.TryGetProperty("bar") with
| (true, _), (true, _) -> // both present
| _ -> // at least one missing
```

### String Interpolation

Always specify the format specifier (e.g., `%s`, `%i`, `%f`) in string interpolation to ensure values are formatted correctly and to prevent accidentally formatting an entire object instead of a specific property:

```fsharp
// Bad - object gets formatted instead of its properties
let user = { Name = "Alice"; Age = 30 }
let message = $"User: {user}"

// Good - explicit format specifiers
let user = { Name = "Alice"; Age = 30 }
let message = $"User: %s{user.Name} (age %i{user.Age})"
```
