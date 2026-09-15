using System.Threading.Tasks;
using ExhaustiveMatching.Analyzer.Testing.Helpers;
using ExhaustiveMatching.Analyzer.Testing.Verifiers;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace ExhaustiveMatching.Analyzer.Tests
{
    public class SwitchStatementAnalyzerTests : DiagnosticVerifier
    {
        [Fact]
        public async Task SwitchOnEnumThrowingInvalidEnumIsNotExhaustiveReportsDiagnostic()
        {
            const string args = "DayOfWeek dayOfWeek";
            const string test = @"
        ◊1⟦switch⟧ (dayOfWeek)
        {
            default:
                throw new InvalidEnumArgumentException(nameof(dayOfWeek), (int)dayOfWeek, typeof(DayOfWeek));
            case DayOfWeek.Monday:
            case DayOfWeek.Tuesday:
            case DayOfWeek.Wednesday:
            case DayOfWeek.Thursday:
                // Omitted Friday
                Console.WriteLine(""Weekday"");
                break;
            case DayOfWeek.Saturday:
                // Omitted Sunday
                Console.WriteLine(""Weekend"");
                break;
        }";

            var source = CodeContext.Basic(args, test);
            var expectedFriday = DiagnosticResult
                                 .Error("EM0001", "Enum value not handled by switch: System.DayOfWeek.Friday")
                                 .AddLocation(source, 1);
            var expectedSunday = DiagnosticResult
                                 .Error("EM0001", "Enum value not handled by switch: System.DayOfWeek.Sunday")
                                 .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expectedFriday, expectedSunday);
        }

        [Fact]
        public async Task SwitchOnEnumThrowingExhaustiveMatchFailedIsNotExhaustiveReportsDiagnostic()
        {
            const string args = "DayOfWeek dayOfWeek";
            const string test = @"
        ◊1⟦switch⟧ (dayOfWeek)
        {
            default:
                throw ExhaustiveMatch.Failed(dayOfWeek);
            case DayOfWeek.Monday:
            case DayOfWeek.Tuesday:
            case DayOfWeek.Wednesday:
            case DayOfWeek.Thursday:
                // Omitted Friday
                Console.WriteLine(""Weekday"");
                break;
            case DayOfWeek.Saturday:
                // Omitted Sunday
                Console.WriteLine(""Weekend"");
                break;
        }";

            var source = CodeContext.Basic(args, test);
            var expectedFriday = DiagnosticResult
                                 .Error("EM0001", "Enum value not handled by switch: System.DayOfWeek.Friday")
                                 .AddLocation(source, 1);
            var expectedSunday = DiagnosticResult
                                 .Error("EM0001", "Enum value not handled by switch: System.DayOfWeek.Sunday")
                                 .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expectedFriday, expectedSunday);
        }

        [Fact]
        public async Task SwitchOnNullableEnumIsNotExhaustiveReportsDiagnostic()
        {
            const string args = "DayOfWeek? dayOfWeek";
            const string test = @"
        ◊1⟦switch⟧ (dayOfWeek)
        {
            default:
                throw ExhaustiveMatch.Failed(dayOfWeek);
            case DayOfWeek.Monday:
            case DayOfWeek.Tuesday:
            case DayOfWeek.Wednesday:
            case DayOfWeek.Thursday:
                // Omitted Friday
                Console.WriteLine(""Weekday"");
                break;
            case DayOfWeek.Saturday:
                // Omitted Sunday
                Console.WriteLine(""Weekend"");
                break;
        }";

            var source = CodeContext.Basic(args, test);
            var expectedNull = DiagnosticResult
                               .Error("EM0002", "null value not handled by switch")
                               .AddLocation(source, 1);
            var expectedFriday = DiagnosticResult
                                 .Error("EM0001", "Enum value not handled by switch: System.DayOfWeek.Friday")
                                 .AddLocation(source, 1);
            var expectedSunday = DiagnosticResult
                                 .Error("EM0001", "Enum value not handled by switch: System.DayOfWeek.Sunday")
                                 .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expectedNull, expectedFriday, expectedSunday);
        }

        [Fact]
        public async Task LogicalEnumPatternsCoverDeclaredValues()
        {
            const string args = "CoinFlip coinFlip";
            const string test = @"
        switch (coinFlip)
        {
            case CoinFlip.Heads or CoinFlip.Tails:
                Console.WriteLine(coinFlip);
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.CoinFlip(args, test));
        }

        [Fact]
        public async Task EnumDeclarationPatternCoversDeclaredValues()
        {
            const string args = "CoinFlip coinFlip";
            const string test = @"
        switch (coinFlip)
        {
            case CoinFlip value:
                Console.WriteLine(value);
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.CoinFlip(args, test));
        }

        [Fact]
        public async Task EnumBaseTypePatternCoversDeclaredValues()
        {
            const string args = "CoinFlip coinFlip";
            const string test = @"
        switch (coinFlip)
        {
            case Enum:
                Console.WriteLine(""enum"");
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.CoinFlip(args, test));
        }

        [Fact]
        public async Task NullableEnumLogicalPatternCoversNull()
        {
            const string args = "CoinFlip? coinFlip";
            const string test = @"
        switch (coinFlip)
        {
            case CoinFlip.Heads or null:
                Console.WriteLine(""Heads or null"");
                break;
            case CoinFlip.Tails:
                Console.WriteLine(""Tails"");
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.CoinFlip(args, test));
        }

        [Fact]
        public async Task EnumNotPatternReportsExcludedValue()
        {
            const string args = "CoinFlip coinFlip";
            const string test = @"
        ◊1⟦switch⟧ (coinFlip)
        {
            case not CoinFlip.Heads:
                Console.WriteLine(""not heads"");
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            var source = CodeContext.CoinFlip(args, test);
            var expected = DiagnosticResult
                           .Error("EM0001", "Enum value not handled by switch: CoinFlip.Heads")
                           .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }

        [Fact]
        public async Task EnumRelationalPatternCoversRange()
        {
            const string args = "RangeEnum value";
            const string test = @"
        switch (value)
        {
            case >= RangeEnum.Low and < RangeEnum.High:
                Console.WriteLine(""range"");
                break;
            case RangeEnum.High:
                Console.WriteLine(""high"");
                break;
            default:
                throw ExhaustiveMatch.Failed(value);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.RangeEnum(args, test));
        }

        [Fact]
        public async Task WideEnumValuesUseExactUnderlyingType()
        {
            const string args = "WideEnum value";
            const string test = @"
        switch (value)
        {
            case WideEnum.Low:
                Console.WriteLine(""low"");
                break;
            case WideEnum.High:
                Console.WriteLine(""high"");
                break;
            default:
                throw ExhaustiveMatch.Failed(value);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.WideEnum(args, test));
        }

        [Fact]
        public async Task UnsignedWideEnumValuesUseExactUnderlyingType()
        {
            const string args = "UnsignedWideEnum value";
            const string test = @"
        switch (value)
        {
            case UnsignedWideEnum.Low:
                Console.WriteLine(""low"");
                break;
            case UnsignedWideEnum.High:
                Console.WriteLine(""high"");
                break;
            default:
                throw ExhaustiveMatch.Failed(value);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.WideEnum(args, test));
        }

        [Fact]
        public async Task GuardedEnumPatternDoesNotCoverValue()
        {
            const string args = "CoinFlip coinFlip";
            const string test = @"
        ◊1⟦switch⟧ (coinFlip)
        {
            case CoinFlip.Heads ◊2⟦when true⟧:
                Console.WriteLine(""Heads"");
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            var source = CodeContext.CoinFlip(args, test);
            var expectedHeads = DiagnosticResult
                                .Error("EM0001", "Enum value not handled by switch: CoinFlip.Heads")
                                .AddLocation(source, 1);
            var expectedTails = DiagnosticResult
                                .Error("EM0001", "Enum value not handled by switch: CoinFlip.Tails")
                                .AddLocation(source, 1);
            var expectedGuard = DiagnosticResult
                                .Error("EM0100", "When guard is not supported in an exhaustive switch")
                                .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expectedHeads, expectedTails, expectedGuard);
        }

        [Fact]
        public async Task NullableEnumCastNullCaseIsRecognized()
        {
            const string args = "CoinFlip? coinFlip";
            const string test = @"
        switch (coinFlip)
        {
            case (CoinFlip?)null:
                Console.WriteLine(""null"");
                break;
            case CoinFlip.Heads:
                Console.WriteLine(""Heads"");
                break;
            case CoinFlip.Tails:
                Console.WriteLine(""Tails"");
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.CoinFlip(args, test));
        }

        [Fact]
        public async Task NullableEnumParenthesizedNullCaseIsRecognized()
        {
            const string args = "CoinFlip? coinFlip";
            const string test = @"
        switch (coinFlip)
        {
            case (null):
                Console.WriteLine(""null"");
                break;
            case CoinFlip.Heads:
                Console.WriteLine(""Heads"");
                break;
            case CoinFlip.Tails:
                Console.WriteLine(""Tails"");
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.CoinFlip(args, test));
        }

        [Fact]
        public async Task NullableEnumVarPatternCoversNullAndValues()
        {
            const string args = "CoinFlip? coinFlip";
            const string test = @"
        switch (coinFlip)
        {
            case var value:
                Console.WriteLine(value);
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.CoinFlip(args, test));
        }

        [Fact]
        public async Task NullableEnumNotNullPatternCoversValuesButNotNull()
        {
            const string args = "CoinFlip? coinFlip";
            const string test = @"
        ◊1⟦switch⟧ (coinFlip)
        {
            case not null:
                Console.WriteLine(""value"");
                break;
            default:
                throw ExhaustiveMatch.Failed(coinFlip);
        }";

            var source = CodeContext.CoinFlip(args, test);
            var expected = DiagnosticResult
                           .Error("EM0002", "null value not handled by switch")
                           .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }

        [Fact]
        public async Task SwitchOnClosedThrowingExhaustiveMatchFailedIsNotExhaustiveReportsDiagnostic()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case Square square:
                Console.WriteLine(""Square: "" + square);
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.Shapes(args, test);
            var expectedCircle = DiagnosticResult
                                 .Error("EM0003", "Subtype not handled by switch: TestNamespace.Circle")
                                 .AddLocation(source, 1);
            var expectedTriangle = DiagnosticResult
                                   .Error("EM0003", "Subtype not handled by switch: TestNamespace.Triangle")
                                   .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expectedCircle, expectedTriangle);
        }

        [Fact]
        public async Task ClosedOrPatternCoversCases()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case Square or Circle:
                Console.WriteLine(""known"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.Shapes(args, test);
            var expected = DiagnosticResult
                           .Error("EM0003", "Subtype not handled by switch: TestNamespace.Triangle")
                           .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }

        [Fact]
        public async Task ClosedAndPatternUsesIntersection()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case Square and Shape:
                Console.WriteLine(""square"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.Shapes(args, test);
            var expectedCircle = DiagnosticResult
                                 .Error("EM0003", "Subtype not handled by switch: TestNamespace.Circle")
                                 .AddLocation(source, 1);
            var expectedTriangle = DiagnosticResult
                                   .Error("EM0003", "Subtype not handled by switch: TestNamespace.Triangle")
                                   .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expectedCircle, expectedTriangle);
        }

        [Fact]
        public async Task ClosedNotPatternUsesClassComplement()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case not Square:
                Console.WriteLine(""not square"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.Shapes(args, test);
            var expected = DiagnosticResult
                           .Error("EM0003", "Subtype not handled by switch: TestNamespace.Square")
                           .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }

        [Fact]
        public async Task ClosedNotNullPatternCoversNonNullCases()
        {
            const string args = "Shape shape";
            const string test = @"
        switch (shape)
        {
            case not null:
                Console.WriteLine(""non-null"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.Shapes(args, test));
        }

        [Fact]
        public async Task ClosedParenthesizedPatternCoversCases()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case (Square or Circle):
                Console.WriteLine(""square or circle"");
                break;
            case Triangle:
                Console.WriteLine(""triangle"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.Shapes(args, test));
        }

        [Fact]
        public async Task ClosedEmptyPropertyPatternCoversCases()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case Shape { }:
                Console.WriteLine(""shape"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.Shapes(args, test));
        }

        [Fact]
        public async Task ClosedConstrainedPropertyPatternIsConservative()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case ◊2⟦Square { Value: 1 }⟧:
                Console.WriteLine(""one"");
                break;
            case Circle:
                Console.WriteLine(""circle"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.ShapesWithProperty(args, test);
            var expectedMissing = DiagnosticResult
                                  .Error("EM0003", "Subtype not handled by switch: TestNamespace.Square")
                                  .AddLocation(source, 1);
            var expectedUnsupported = DiagnosticResult
                                     .Error("EM0101", "Case pattern not supported in exhaustive switch: Square { Value: 1 }")
                                     .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expectedMissing, expectedUnsupported);
        }

        [Fact]
        public async Task MalformedRecursivePatternDoesNotSuppressMissingCase()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case ◊2⟦Square { ◊3⟦Missing⟧: var _ }⟧:
                Console.WriteLine(""invalid"");
                break;
            case Circle:
                Console.WriteLine(""circle"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.ShapesWithProperty(args, test);
            var expectedMissing = DiagnosticResult
                                  .Error("EM0003", "Subtype not handled by switch: TestNamespace.Square")
                                  .AddLocation(source, 1);
            var expectedUnsupported = DiagnosticResult
                                     .Error("EM0101", "Case pattern not supported in exhaustive switch: Square { Missing: var _ }")
                                     .AddLocation(source, 2);
            var compileError = DiagnosticResult
                               .Error("CS0117", "'Square' does not contain a definition for 'Missing'")
                               .AddLocation(source, 3);

            await VerifyCSharpDiagnosticsAsync(source, expectedMissing, expectedUnsupported, compileError);
        }

        [Fact]
        public async Task ClosedTotalPropertyPatternCoversCase()
        {
            const string args = "Shape shape";
            const string test = @"
        switch (shape)
        {
            case Square { Value: var _ }:
                Console.WriteLine(""square"");
                break;
            case Circle:
                Console.WriteLine(""circle"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.ShapesWithProperty(args, test));
        }

        [Fact]
        public async Task ClosedTotalPositionalPatternCoversCases()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error(var _):
                Console.WriteLine(""error"");
                break;
            case Result<string, string>.Success(var _):
                Console.WriteLine(""success"");
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.ResultRecord(args, test));
        }

        [Fact]
        public async Task ClosedConstrainedPositionalPatternIsConservative()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case ◊2⟦Result<string, string>.Error(""known"")⟧:
                Console.WriteLine(""error"");
                break;
            case Result<string, string>.Success(var _):
                Console.WriteLine(""success"");
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.ResultRecord(args, test);
            var expectedMissing = DiagnosticResult
                                  .Error("EM0003", "Subtype not handled by switch: TestNamespace.Error")
                                  .AddLocation(source, 1);
            var expectedUnsupported = DiagnosticResult
                                     .Error("EM0101", "Case pattern not supported in exhaustive switch: Result<string, string>.Error(\"known\")")
                                     .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expectedMissing, expectedUnsupported);
        }

        [Fact]
        public async Task ClosedVarPatternCoversCases()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case var value:
                Console.WriteLine(value);
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.Shapes(args, test));
        }

        [Fact]
        public async Task ClosedListPatternIsConservative()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊1⟦switch⟧ (shape)
        {
            case ◊2⟦[1, ..]⟧:
                Console.WriteLine(""one"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.ListShapes(args, test);
            var expectedCircle = DiagnosticResult
                                 .Error("EM0003", "Subtype not handled by switch: TestNamespace.Circle")
                                 .AddLocation(source, 1);
            var expectedSquare = DiagnosticResult
                                 .Error("EM0003", "Subtype not handled by switch: TestNamespace.Square")
                                 .AddLocation(source, 1);
            var expectedUnsupported = DiagnosticResult
                                     .Error("EM0101", "Case pattern not supported in exhaustive switch: [1, ..]")
                                     .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expectedCircle, expectedSquare, expectedUnsupported);
        }

        [Fact]
        public async Task ListPatternIsConservativeAndDoesNotCrash()
        {
            const string args = "int[] values";
            const string test = @"
        switch (◊1⟦values⟧)
        {
            case ◊2⟦[1, ..]⟧:
                Console.WriteLine(""one"");
                break;
            default:
                throw ExhaustiveMatch.Failed(values);
        }";

            var source = CodeContext.Basic(args, test);
            var expectedOpen = DiagnosticResult
                               .Error("EM0102", "Exhaustive switch must be on enum or closed type, was on: int[]")
                               .AddLocation(source, 1);
            var expectedUnsupported = DiagnosticResult
                                     .Error("EM0101", "Case pattern not supported in exhaustive switch: [1, ..]")
                                     .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expectedOpen, expectedUnsupported);
        }

        [Fact]
        public async Task InterfaceNotPatternIsConservative()
        {
            const string source = @"using ExhaustiveMatching;

namespace TestNamespace
{
    [Closed(typeof(ICat), typeof(IDog))]
    public interface IAnimal { }
    public interface ICat : IAnimal { }
    public interface IDog : IAnimal { }

    class TestClass
    {
        void TestMethod(IAnimal animal)
        {
            ◊1⟦switch⟧ (animal)
            {
                case ◊2⟦not ICat⟧:
                    break;
                default:
                    throw ExhaustiveMatch.Failed(animal);
            }
        }
    }
}";

            var expectedCat = DiagnosticResult
                             .Error("EM0003", "Subtype not handled by switch: TestNamespace.ICat")
                             .AddLocation(source, 1);
            var expectedDog = DiagnosticResult
                             .Error("EM0003", "Subtype not handled by switch: TestNamespace.IDog")
                             .AddLocation(source, 1);
            var expectedUnsupported = DiagnosticResult
                                     .Error("EM0101", "Case pattern not supported in exhaustive switch: not ICat")
                                     .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expectedCat, expectedDog, expectedUnsupported);
        }

        [Fact]
        public async Task ExhaustiveObjectSwitchAllowsNull()
        {
            const string args = "Shape shape";
            const string test = @"
        switch (shape)
        {
            case Square square:
                Console.WriteLine(""Square: "" + square);
                break;
            case Circle circle:
                Console.WriteLine(""Circle: "" + circle);
                break;
            case Triangle triangle:
                Console.WriteLine(""Triangle: "" + triangle);
                break;
            case null: // checking this is allowed
                Console.WriteLine(""null"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.Shapes(args, test));
        }

        [Fact]
        public async Task ExhaustiveObjectSwitchAllowsLabelSyntax()
        {
            const string args = "Shape shape";
            const string test = @"
        switch (shape)
        {
            case Square square:
                Console.WriteLine(""Square: "" + square);
                break;
            case Circle circle:
                Console.WriteLine(""Circle: "" + circle);
                break;
            case Triangle: // label syntax
                Console.WriteLine(""Triangle!"");
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            await VerifyCSharpDiagnosticsAsync(CodeContext.Shapes(args, test));
        }

        [Fact]
        public async Task UnsupportedCaseClauses()
        {
            const string args = "Shape shape";
            const string test = @"
        ◊3⟦switch⟧ (shape)
        {
            case Square square:
                Console.WriteLine(""Square: "" + square);
                break;
            case Circle circle:
                Console.WriteLine(""Circle: "" + circle);
                break;
            case Triangle triangle ◊1⟦when true⟧:
                Console.WriteLine(""Triangle: "" + triangle);
                break;
            case ◊2⟦12⟧:
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.Shapes(args, test);
            var expectedTriangle = DiagnosticResult
                            .Error("EM0003", "Subtype not handled by switch: TestNamespace.Triangle")
                            .AddLocation(source, 3);
            var expected1 = DiagnosticResult
                            .Error("EM0100", "When guard is not supported in an exhaustive switch")
                            .AddLocation(source, 1);
            var compileError = DiagnosticResult
                               .Error("CS0029", "Cannot implicitly convert type 'int' to 'TestNamespace.Shape'")
                               .AddLocation(source, 2);
            var expected2 = DiagnosticResult
                            .Error("EM0101", "Case pattern not supported in exhaustive switch: 12")
                            .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expectedTriangle, expected1, compileError, expected2);
        }

        [Fact]
        public async Task SwitchOnNonClosedType()
        {
            const string args = "object o";
            const string test = @"
        switch (◊1⟦o⟧)
        {
            case string s:
                Console.WriteLine(""string: "" + s);
                break;
            case Triangle triangle ◊2⟦when true⟧:
                Console.WriteLine(""Triangle: "" + triangle);
                break;
            case ◊3⟦12⟧:
                break;
            default:
                throw ExhaustiveMatch.Failed(o);
        }";

            var source = CodeContext.Shapes(args, test);
            var expected1 = DiagnosticResult
                            .Error("EM0102", "Exhaustive switch must be on enum or closed type, was on: System.Object")
                            .AddLocation(source, 1);

            // Still reports these errors
            var expected2 = DiagnosticResult
                            .Error("EM0100", "When guard is not supported in an exhaustive switch")
                            .AddLocation(source, 2);
            var expected3 = DiagnosticResult
                            .Error("EM0101", "Case pattern not supported in exhaustive switch: 12")
                            .AddLocation(source, 3);

            await VerifyCSharpDiagnosticsAsync(source, expected1, expected2, expected3);
        }

        [Fact]
        public async Task ModernTypePatternIsRecognizedOnOpenType()
        {
            const string args = "object o";
            const string test = @"
        switch (◊1⟦o⟧)
        {
            case ◊2⟦string⟧:
                Console.WriteLine(""string"");
                break;
            default:
                throw ExhaustiveMatch.Failed(o);
        }";

            var source = CodeContext.Basic(args, test);
            var expected = DiagnosticResult
                           .Error("EM0102", "Exhaustive switch must be on enum or closed type, was on: System.Object")
                           .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }

        [Fact]
        public async Task ErrorForMatchOnTypesOutsideOfHierarchy()
        {
            const string args = "Shape shape";
            const string test = @"
        switch (shape)
        {
            case Square square:
                Console.WriteLine(""Square: "" + square);
                break;
            case Circle circle:
                Console.WriteLine(""Circle: "" + circle);
                break;
            case ◊1⟦EquilateralTriangle equilateralTriangle⟧:
                Console.WriteLine(""EquilateralTriangle: "" + equilateralTriangle);
                break;
            case Triangle triangle:
                Console.WriteLine(""Triangle: "" + triangle);
                break;
            case ◊3⟦◊2⟦string⟧ s⟧:
                Console.WriteLine(""string: "" + s);
                break;
            default:
                throw ExhaustiveMatch.Failed(shape);
        }";

            var source = CodeContext.Shapes(args, test);
            var expected1 = DiagnosticResult
                            .Error("EM0103", "TestNamespace.EquilateralTriangle is not a case type inheriting from type being matched: TestNamespace.Shape")
                            .AddLocation(source, 1);
            var compileError = DiagnosticResult
                               .Error("CS8121", "An expression of type 'Shape' cannot be handled by a pattern of type 'string'.")
                               .AddLocation(source, 2);
            var expected2 = DiagnosticResult
                            .Error("EM0103", "System.String is not a case type inheriting from type being matched: TestNamespace.Shape")
                            .AddLocation(source, 3);

            await VerifyCSharpDiagnosticsAsync(source, expected1, compileError, expected2);
        }


        [Fact]
        public async Task HandlesMultipleClosedAttributes()
        {
            const string source = @"using System;
using ExhaustiveMatching;

namespace TestNamespace
{
    [Closed(
        typeof(Square),
        typeof(Circle))]
    [◊1⟦Closed(typeof(Triangle))⟧]
    public abstract class Shape { }
    public class Square : Shape { }
    public class Circle : Shape { }
    public class Triangle : Shape { }

    class TestClass
    {
        void TestMethod(Shape shape)
        {
            switch (shape)
            {
                default:
                    throw ExhaustiveMatch.Failed(shape);
                case Square square:
                    Console.WriteLine(""Square: "" + square);
                    break;
                case Circle circle:
                    Console.WriteLine(""Circle: "" + circle);
                    break;
                case Triangle triangle:
                    Console.WriteLine(""Triangle: "" + triangle);
                    break;
            }
        }
    }
}";
            var expected1 = DiagnosticResult
                            .Error("EM0104", "Duplicate 'Closed' attribute")
                            .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected1);
        }

        /// <summary>
        /// Regression test for an issue where `typeof()` as a case type would
        /// cause all switches on that type to report a missing case with no
        /// type listed. (It was the error type.)
        /// </summary>
        [Fact]
        public async Task EmptyTypeofDoesNotCauseMissingCase()
        {
            const string source = @"using System;
using ExhaustiveMatching;

namespace TestNamespace
{
    [Closed(
        typeof(Square),
        typeof(◊1⟦⟧),
        typeof(Circle))]
    public abstract class Shape { }
    public class Square : Shape { }
    public class Circle : Shape { }

    class TestClass
    {
        void TestMethod(Shape shape)
        {
            switch (shape)
            {
                case Square square:
                    Console.WriteLine(""Square: "" + square);
                    break;
                case Circle circle:
                    Console.WriteLine(""Circle: "" + circle);
                    break;
                default:
                    throw ExhaustiveMatch.Failed(shape);
            }
        }
    }
}";

            var compileError = DiagnosticResult
                               .Error("CS1031", "Type expected")
                               .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, compileError);
        }

        /// <summary>
        /// Regression test for an issue where using a closed interface type in
        /// the middle of a case hierarchy gave EM0013 that the type was not a
        /// case type.
        /// </summary>
        [Fact]
        public async Task MultipleInterfaceLevels()
        {
            const string test = @"using System;
using ExhaustiveMatching;
namespace TestNamespace
{
    [Closed(typeof(IKeywordToken))]
    public interface IToken { }
    [Closed(typeof(IForeachKeyword))]
    public interface IKeywordToken : IToken { }
    public interface IForeachKeyword : IKeywordToken { }

    class TestClass
    {
        void TestMethod(IToken token)
        {
            switch (token)
            {
                default:
                    throw ExhaustiveMatch.Failed(token);
                case IKeywordToken _:
                    Console.WriteLine(""foreach"");
                    break;
            }
        }
    }
}";

            await VerifyCSharpDiagnosticsAsync(test);
        }

        /// <summary>
        /// Regression test for infinite loop when a type listed itself as one
        /// of its case types and was used in a switch.
        /// </summary>
        [Fact]
        public async Task SelfTypeAsCaseType()
        {
            const string source = @"using System;
using ExhaustiveMatching;

namespace TestNamespace
{
    [Closed(typeof(◊1⟦IToken⟧), typeof(IKeywordToken))]
    public interface IToken { }
    public interface IKeywordToken : IToken { }

    class TestClass
    {
        void TestMethod(IToken token)
        {
            switch (token)
            {
                case IKeywordToken _:
                    Console.WriteLine(""foreach"");
                    break;
                default:
                    throw ExhaustiveMatch.Failed(token);
            }
        }
    }
}";
            var expected = DiagnosticResult
                           .Error("EM0013", "Closed type case is not a subtype: TestNamespace.IToken")
                           .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }


        [Fact]
        public async Task SwitchOnStructurallyClosedThrowingExhaustiveMatchFailedIsNotExhaustiveReportsDiagnostic()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error error:
                Console.WriteLine(""Error: "" + error);
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.Result(args, test);
            var expectedSuccess = DiagnosticResult
                .Error("EM0003", "Subtype not handled by switch: TestNamespace.Success")
                .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expectedSuccess);
        }

        [Fact]
        public async Task SwitchOnStructurallyClosedThrowingExhaustiveMatchDoesNotReportsDiagnostic()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error error:
                Console.WriteLine(""Error: "" + error);
                break;
            case Result<string, string>.Success success:
                Console.WriteLine(""Success: "" + success);
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.Result(args, test);

            await VerifyCSharpDiagnosticsAsync(source);
        }

        [Fact]
        public async Task SwitchOnStructurallyClosedWithLabelThrowingExhaustiveMatchDoesNotReportsDiagnostic()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error error:
                Console.WriteLine(""Error: "" + error);
                break;
            case Result<string, string>.Success:
                Console.WriteLine(""Success!"");
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.Result(args, test);

            await VerifyCSharpDiagnosticsAsync(source);
        }

        [Fact]
        public async Task SwitchOnStructurallyClosedThrowingExhaustiveMatchAllowNull()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error error:
                Console.WriteLine(""Error: "" + error);
                break;
            case Result<string, string>.Success success:
                Console.WriteLine(""Success: "" + success);
                break;
            case null:
                Console.WriteLine(""null"");
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.Result(args, test);

            await VerifyCSharpDiagnosticsAsync(source);
        }

        [Fact]
        public async Task SwitchOnStructurallyClosedRecordThrowingExhaustiveMatchFailedIsNotExhaustiveReportsDiagnostic()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error error:
                Console.WriteLine(""Error: "" + error);
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.ResultRecord(args, test);
            var expectedSuccess = DiagnosticResult
                .Error("EM0003", "Subtype not handled by switch: TestNamespace.Success")
                .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expectedSuccess);
        }

        [Fact]
        public async Task SwitchOnStructurallyClosedRecordThrowingExhaustiveMatchDoesNotReportsDiagnostic()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error error:
                Console.WriteLine(""Error: "" + error);
                break;
            case Result<string, string>.Success success:
                Console.WriteLine(""Success: "" + success);
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.ResultRecord(args, test);

            await VerifyCSharpDiagnosticsAsync(source);
        }

        [Fact]
        public async Task SwitchOnStructurallyClosedRecordThrowingExhaustiveMatchAllowNull()
        {
            const string args = "Result<string, string> result";
            const string test = @"
        ◊1⟦switch⟧ (result)
        {
            case Result<string, string>.Error error:
                Console.WriteLine(""Error: "" + error);
                break;
            case Result<string, string>.Success success:
                Console.WriteLine(""Success: "" + success);
                break;
            case null:
                Console.WriteLine(""null"");
                break;
            default:
                throw ExhaustiveMatch.Failed(result);
        }";

            var source = CodeContext.ResultRecord(args, test);

            await VerifyCSharpDiagnosticsAsync(source);
        }

        [Fact]
        public async Task SwitchOnStruct()
        {
            const string args = "HashCode hashCode";
            const string test = @"
        switch (◊1⟦hashCode⟧)
        {
            default:
                throw ExhaustiveMatch.Failed(hashCode);
            case HashCode code:
                Console.WriteLine(""Hashcode: "" + code);
                break;
        }";

            var source = CodeContext.Basic(args, test);
            var expected = DiagnosticResult.Error("EM0102", "Exhaustive switch must be on enum or closed type, was on: System.HashCode")
                                                 .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }

        [Fact]
        public async Task SwitchOnNullableStruct()
        {
            const string args = "HashCode? hashCode";
            const string test = @"
        switch (◊1⟦hashCode⟧)
        {
            default:
                throw ExhaustiveMatch.Failed(hashCode);
            case null:
                Console.WriteLine(""null"");
                break;
            case HashCode code:
                Console.WriteLine(""Hashcode: "" + code);
                break;
        }";

            var source = CodeContext.Basic(args, test);
            var expected = DiagnosticResult
                           .Error("EM0102", "Exhaustive switch must be on enum or closed type, was on: System.Nullable")
                           .AddLocation(source, 1);

            await VerifyCSharpDiagnosticsAsync(source, expected);
        }

        [Fact]
        public async Task SwitchOnTuple()
        {
            const string args = "Shape shape1, Shape shape2";
            const string test = @"
        switch (◊1⟦(shape1, shape2)⟧)
        {
            case ◊2⟦(Square square1, Square square2)⟧:
                Console.WriteLine(""Square: "" + square1);
                Console.WriteLine(""Square: "" + square2);
                break;
            default:
                throw ExhaustiveMatch.Failed((shape1, shape2));
        }";

            var source = CodeContext.Shapes(args, test);
            var expected1 = DiagnosticResult
                            .Error("EM0102", "Exhaustive switch must be on enum or closed type, was on: System.ValueTuple")
                            .AddLocation(source, 1);
            var expected2 = DiagnosticResult
                            .Error("EM0101", "Case pattern not supported in exhaustive switch: (Square square1, Square square2)")
                            .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expected1, expected2);
        }

        [Fact]
        public async Task SwitchOnNullableTuple()
        {
            const string args = "Shape shape1, Shape shape2";
            const string test = @"
        (Shape, Shape)? value = (shape1, shape2);
        switch (◊1⟦value⟧)
        {
            case ◊2⟦(Square square1, Square square2)⟧:
                Console.WriteLine(""Square: "" + square1);
                Console.WriteLine(""Square: "" + square2);
                break;
            default:
                throw ExhaustiveMatch.Failed(value);
        }";

            var source = CodeContext.Shapes(args, test);
            // TODO type name is bad
            var expected1 = DiagnosticResult
                            .Error("EM0102", "Exhaustive switch must be on enum or closed type, was on: System.Nullable")
                            .AddLocation(source, 1);
            var expected2 = DiagnosticResult
                            .Error("EM0101",
                                "Case pattern not supported in exhaustive switch: (Square square1, Square square2)")
                            .AddLocation(source, 2);

            await VerifyCSharpDiagnosticsAsync(source, expected1, expected2);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
            => new ExhaustiveMatchAnalyzer();
    }
}
