﻿namespace BlazorRepl.Core
{
    public static class CoreConstants
    {
        public const string MainComponentFilePath = "__Main.razor";
        public const string StartupClassFilePath = "Startup.cs";
        public const string MainComponentDefaultFileContent = @"<h1>Hello, Blazor REPL!</h1>

@code {

}
";

        public const string StartupClassDefaultFileContent = @"namespace BlazorRepl.UserComponents  
{
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;  
    using Microsoft.Extensions.DependencyInjection;

    public static class Startup  
    {  
        public static void Configure(WebAssemblyHostBuilder builder)  
        {
        }
    }
}
";

        public const string DefaultUserComponentsAssemblyBytes =
            "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ACIAIABiAHUAdAB0AG8AbgAgAG8AcgAgAHAAcgBlAHMAcwBpAG4AZwAgAEMAdAByAGwAKwBTAC4APAAvAHAAPgAKAAoAAD08AGgAMgA+AFMAaABhAHIAZQAgAHkAbwB1AHIAIABzAG4AaQBwAHAAZQB0AHMAIQA8AC8AaAAyAD4ACgAAczwAcAA+AFMAaABhAHIAZQAgAHkAbwB1AHIAIABzAG4AaQBwAHAAZQB0ACAAZQBhAHMAaQBsAHkAIABiAHkAIABmAG8AbABsAG8AdwBpAG4AZwAgAHQAaABlACAAcwB0AGUAcABzADoAPAAvAHAAPgAKAACBTTwAdQBsAD4APABsAGkAPgBDAGwAaQBjAGsAIAB0AGgAZQAgACIAUwBBAFYARQAiACAAYgB1AHQAdABvAG4APAAvAGwAaQA+AAoAIAAgACAAIAA8AGwAaQA+AEMAbwBuAGYAaQByAG0AIAB0AGgAYQB0ACAAeQBvAHUAIABhAGcAcgBlAGUAIAB3AGkAdABoACAAdABoAGUAIAB0AGUAcgBtAHMAPAAvAGwAaQA+AAoAIAAgACAAIAA8AGwAaQA+AEMAbwBwAHkAIAB0AGgAZQAgAFUAUgBMACAAbwBmACAAdABoAGUAIABzAG4AaQBwAHAAZQB0ACAAYQBuAGQAIABwAGEAcwB0AGUAIABpAHQAIAB3AGgAZQByAGUAdgBlAHIAIAB5AG8AdQAgAG4AZQBlAGQAPAAvAGwAaQA+ADwALwB1AGwAPgAKAAoAAFM8AGgAMgA+AFcAaABhAHQAIABhAHIAZQAgAHQAaABlACAAZQBkAGkAdABvAHIAJwBzACAAZgBlAGEAdAB1AHIAZQBzAD8APAAvAGgAMgA+AAoAAYLHPABwAD4AVwBlACAAYQByAGUAIAB1AHMAaQBuAGcAIABNAGkAYwByAG8AcwBvAGYAdAAnAHMAIAA8AGEAIAB0AGEAcgBnAGUAdAA9ACIAXwBiAGwAYQBuAGsAIgAgAGgAcgBlAGYAPQAiAGgAdAB0AHAAcwA6AC8ALwBtAGkAYwByAG8AcwBvAGYAdAAuAGcAaQB0AGgAdQBiAC4AaQBvAC8AbQBvAG4AYQBjAG8ALQBlAGQAaQB0AG8AcgAvACIAPgBNAG8AbgBhAGMAbwAgAEUAZABpAHQAbwByADwALwBhAD4ALgAgAEkAdAAgAGkAcwAgAHQAaABlACAAYwBvAGQAZQAgAGUAZABpAHQAbwByACAAdABoAGEAdAAgAHAAbwB3AGUAcgBzACAAVgBTACAAQwBvAGQAZQAuACAAWQBvAHUAIABjAGEAbgAgAGEAYwBjAGUAcwBzACAAaQB0AHMAIABDAG8AbQBtAGEAbgBkACAAUABhAGwAZQB0AHQAZQAgAGIAeQAgAGYAbwBjAHUAcwBpAG4AZwAgAG8AbgAgAHQAaABlACAAZQBkAGkAdABvAHIAIABhAG4AZAAgAGMAbABpAGMAawBpAG4AZwAgAEYAMQAgAGIAdQB0AHQAbwBuACAAbwBuACAAeQBvAHUAcgAgAGsAZQB5AGIAbwBhAHIAZAAuACAAWQBvAHUAIAB3AGkAbABsACAAcwBlAGUAIAB0AGgAZQAgAGwAaQBzAHQAIABvAGYAIABhAGwAbAAgAGEAdgBhAGkAbABhAGIAbABlACAAYwBvAG0AbQBhAG4AZABzAC4AIABZAG8AdQAgAGMAYQBuACAAdQBzAGUAIAB0AGgAZQAgAGMAbwBtAG0AYQBuAGQAcwAnACAAcwBoAG8AcgB0AGMAdQB0AHMAIAB0AG8AbwAuADwALwBwAD4ACgABaTwAcAA+AFMAbwBtAGUAIABvAGYAIAB0AGgAZQAgAG0AbwBzAHQAIABjAG8AbQBtAG8AbgBsAHkAIAB1AHMAZQBkACAAYwBvAG0AbQBhAG4AZABzACAAYQByAGUAOgA8AC8AcAA+AAoAAIFrPAB1AGwAPgA8AGwAaQA+AEMAdAByAGwAKwBLACAAQwB0AHIAbAArAEMAIABjAG8AbQBtAGUAbgB0AHMAIABvAHUAdAAgAHQAaABlACAAYwB1AHIAcgBlAG4AdAAgAGwAaQBuAGUAPAAvAGwAaQA+AAoAIAAgACAAIAA8AGwAaQA+AEMAdAByAGwAKwBTAGgAaQBmAHQAKwBLACAAZABlAGwAZQB0AGUAcwAgAGEAIABsAGkAbgBlADwALwBsAGkAPgAKACAAIAAgACAAPABsAGkAPgBDAG8AbQBtAGEAbgBkACAAUABhAGwAZQB0AHQAZQAgAC0APgAgAEUAZABpAHQAbwByACAARgBvAG4AdAAgAFoAbwBvAG0AIABJAG4ALwBPAHUAdAAgAGMAaABhAG4AZwBlAHMAIAB0AGgAZQAgAGYAbwBuAHQAIABzAGkAegBlADwALwBsAGkAPgA8AC8AdQBsAD4ACgABgWs8AHAAPgBJAGYAIAB5AG8AdQAgAHcAYQBuAHQAIAB0AG8AIABkAGkAZwAgAGEAIABsAGkAdAB0AGwAZQAgAGQAZQBlAHAAZQByACAAaQBuAHQAbwAgAE0AbwBuAGEAYwBvACAARQBkAGkAdABvAHIAJwBzACAAZgBlAGEAdAB1AHIAZQBzACwAIAB5AG8AdQAgAGMAYQBuACAAZABvACAAcwBvACAAPABhACAAdABhAHIAZwBlAHQAPQAiAF8AYgBsAGEAbgBrACIAIABoAHIAZQBmAD0AIgBoAHQAdABwAHMAOgAvAC8AYwBvAGQAZQAuAHYAaQBzAHUAYQBsAHMAdAB1AGQAaQBvAC4AYwBvAG0ALwBkAG8AYwBzAC8AZQBkAGkAdABvAHIALwBlAGQAaQB0AGkAbgBnAGUAdgBvAGwAdgBlAGQAIgA+AGgAZQByAGUAPAAvAGEAPgAuADwALwBwAD4ACgAKAAExPABoADIAPgBFAG4AagBvAHkAIABjAHIAZQBhAHQAaQBuAGcAIQA8AC8AaAAyAD4AAAAA85G1IggFbUyMt2Jcs4oKpgAEIAEBCAMgAAEFIAEBEREEIAEBDgUgAgEIDgh87IXXvqd5jgituXk4Kd2uYAUgAQESHQgBAAgAAAAAAB4BAAEAVAIWV3JhcE5vbkV4Y2VwdGlvblRocm93cwEIAQACAAAAAAAMAQAHL19fbWFpbgAAAAAAkC4AAAAAAAAAAAAAqi4AAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAJwuAAAAAAAAAAAAAAAAX0NvckRsbE1haW4AbXNjb3JlZS5kbGwAAAAAAP8lACAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAwAAAC8PgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        public const string DefaultRazorFileContentFormat = "<h1>{0}</h1>";

        public static readonly string DefaultCSharpFileContentFormat =
            @$"namespace {CompilationService.DefaultRootNamespace}
{{{{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class {{0}}
    {{{{
    }}}}
}}}}
";
    }
}
