using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data;
using System.Linq;

class SimpleLangInterpreter
{
    static Dictionary<string, object> variables = new Dictionary<string, object>();
    static Dictionary<string, (List<string> parameters, List<string> body)> functions = new Dictionary<string, (List<string>, List<string>)>();
    static readonly HashSet<string> keywords = new HashSet<string>
    {
        "VAR", "IF", "ELSE", "WHILE", "RETURN", "FUNC", "PRINT", "END", "BEGIN"
    };

    static readonly HashSet<string> operators = new HashSet<string>
    {
        "+", "-", "*", "/", "%", "==", "!=", "<", ">", "<=", ">=", "&&", "||", "!", "=", "+=", "-=", "*=", "/="
    };

    static void Main()
    {
        Console.WriteLine("Enter your code (type 'exit' to quit):");
        List<string> functionBuffer = new List<string>();
        bool insideFunction = false;
        Stack<string> blockStack = new Stack<string>();
        string? functionName = null;
        List<string> loopBuffer = new List<string>();
        bool insideLoop = false;
        
        while (true)
        {
            Console.Write("> ");
            string? input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;
            if (input.ToLower() == "exit") break;

            input = input.Trim();
            if (!input.EndsWith(";") && !input.StartsWith("FUNC ") && input != "BEGIN" && input != "END" && !input.StartsWith("WHILE "))
            {
                Console.WriteLine("Syntax Error: Statement must end with semicolon!");
                continue;
            }

            if (input.EndsWith(";"))
            {
                input = input.Substring(0, input.Length - 1);
            }

            if (input.StartsWith("FUNC "))
            {
                string funcDef = input.Substring(5).Trim();
                var match = Regex.Match(funcDef, "^([a-zA-Z_][a-zA-Z0-9_]*)\\s*\\((.*)\\)$");
                if (!match.Success)
                {
                    // Try without parameters
                    match = Regex.Match(funcDef, "^([a-zA-Z_][a-zA-Z0-9_]*)$");
                    if (!match.Success)
                    {
                        Console.WriteLine("Syntax Error: Invalid function definition! Use: FUNC name or FUNC name(param1, param2, ...)");
                        continue;
                    }
                    funcDef = funcDef + "()";
                    match = Regex.Match(funcDef, "^([a-zA-Z_][a-zA-Z0-9_]*)\\s*\\((.*)\\)$");
                }
                
                functionName = match.Groups[1].Value;
                string paramsStr = match.Groups[2].Value;
                List<string> parameters = paramsStr.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                if (functions.ContainsKey(functionName))
                {
                    Console.WriteLine("Syntax Error: Function already defined!");
                    continue;
                }

                insideFunction = true;
                blockStack.Push("FUNC");
                functionBuffer.Clear();
                functionBuffer.Add(input);
                functions[functionName] = (parameters, new List<string>());
            }
            else if (input.StartsWith("WHILE "))
            {
                blockStack.Push("WHILE");
                insideLoop = true;
                if (insideFunction)
                {
                    functionBuffer.Add(input);
                }
            }
            else if (input == "BEGIN")
            {
                blockStack.Push("BEGIN");
                if (insideFunction)
                {
                    functionBuffer.Add(input);
                }
            }
            else if (input == "END")
            {
                if (blockStack.Count == 0)
                {
                    Console.WriteLine("Syntax Error: Unmatched END statement!");
                    continue;
                }
                string blockType = blockStack.Pop();
                if (blockType == "BEGIN")
                {
                    if (insideFunction)
                    {
                        functionBuffer.Add(input);
                    }
                }
                else if (blockType == "WHILE")
                {
                    insideLoop = false;
                    if (insideFunction)
                    {
                        functionBuffer.Add(input);
                    }
                }
                else if (blockType == "FUNC" && functionName != null)
                {
                    functionBuffer.Add(input);
                    var (parameters, _) = functions[functionName];
                    functions[functionName] = (parameters, new List<string>(functionBuffer));
                    insideFunction = false;
                    functionName = null;
                }
            }
            else if (insideFunction)
            {
                functionBuffer.Add(input);
            }
            else if (Regex.IsMatch(input, "^[a-zA-Z_][a-zA-Z0-9_]*\\((.*)\\)$")) // Function call with parameters
            {
                var match = Regex.Match(input, "^([a-zA-Z_][a-zA-Z0-9_]*)\\((.*)\\)$");
                string funcName = match.Groups[1].Value;
                string argsStr = match.Groups[2].Value;
                
                List<string> args = argsStr.Split(',')
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToList();

                object? result = ExecuteFunction(funcName, args);
                if (result != null)
                {
                    Console.WriteLine(result);
                }
            }
            else if (Regex.IsMatch(input, "^[a-zA-Z_][a-zA-Z0-9_]* = [a-zA-Z_][a-zA-Z0-9_]*\\((.*)\\)$")) // Function call with assignment
            {
                var match = Regex.Match(input, "^([a-zA-Z_][a-zA-Z0-9_]*)\\s*=\\s*([a-zA-Z_][a-zA-Z0-9_]*)\\((.*)\\)$");
                string varName = match.Groups[1].Value;
                string funcName = match.Groups[2].Value;
                string argsStr = match.Groups[3].Value;
                
                List<string> args = argsStr.Split(',')
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrEmpty(a))
                    .ToList();

                object? result = ExecuteFunction(funcName, args);
                if (result != null)
                {
                    variables[varName] = result;
                }
            }
            else
            {
                ExecuteLine(input);
            }
        }
    }

    static void ExecuteLines(string input)
    {
        string[] lines = input.Split(new[] { "\n", ";" }, StringSplitOptions.RemoveEmptyEntries);
        Stack<string> blockStack = new Stack<string>();
        List<string> functionBuffer = new List<string>();
        string? functionName = null;
        List<string> loopBuffer = new List<string>();
        bool insideFunction = false;
        bool insideLoop = false;
        
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("/*")) continue;

            if (line.StartsWith("FUNC "))
            {
                string funcDef = line.Substring(5).Trim();
                var match = Regex.Match(funcDef, "^([a-zA-Z_][a-zA-Z0-9_]*)\\s*\\((.*)\\)$");
                if (!match.Success)
                {
                    // Try without parameters
                    match = Regex.Match(funcDef, "^([a-zA-Z_][a-zA-Z0-9_]*)$");
                    if (!match.Success)
                    {
                        Console.WriteLine("Syntax Error: Invalid function definition! Use: FUNC name or FUNC name(param1, param2, ...)");
                        return;
                    }
                    funcDef = funcDef + "()";
                    match = Regex.Match(funcDef, "^([a-zA-Z_][a-zA-Z0-9_]*)\\s*\\((.*)\\)$");
                }
                
                functionName = match.Groups[1].Value;
                string paramsStr = match.Groups[2].Value;
                List<string> parameters = paramsStr.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                if (functions.ContainsKey(functionName))
                {
                    Console.WriteLine("Syntax Error: Function already defined!");
                    return;
                }
                insideFunction = true;
                blockStack.Push("FUNC");
                functionBuffer.Clear();
                functions[functionName] = (parameters, new List<string>());
            }
            else if (line.StartsWith("WHILE "))
            {
                insideLoop = true;
                blockStack.Push("WHILE");
                loopBuffer.Clear();
                loopBuffer.Add(line);
            }
            else if (line == "BEGIN")
            {
                blockStack.Push("BEGIN");
                if (insideFunction)
                {
                    functionBuffer.Add(line);
                }
                else if (insideLoop)
                {
                    loopBuffer.Add(line);
                }
            }
            else if (line == "END")
            {
                if (blockStack.Count == 0)
                {
                    Console.WriteLine("Syntax Error: Unmatched END statement!");
                    return;
                }
                string blockType = blockStack.Pop();
                if (blockType == "FUNC" && functionName != null)
                {
                    var (parameters, _) = functions[functionName];
                    functions[functionName] = (parameters, new List<string>(functionBuffer));
                    insideFunction = false;
                }
                else if (blockType == "WHILE")
                {
                    ExecuteWhileLoop(loopBuffer);
                    insideLoop = false;
                }
                else if (blockType == "BEGIN")
                {
                    if (insideFunction)
                    {
                        functionBuffer.Add(line);
                    }
                    else if (insideLoop)
                    {
                        loopBuffer.Add(line);
                    }
                }
            }
            else if (insideFunction)
            {
                functionBuffer.Add(line);
            }
            else if (insideLoop)
            {
                loopBuffer.Add(line);
            }
            else
            {
                ExecuteLine(line);
            }
        }

        if (blockStack.Count > 0)
        {
            Console.WriteLine("Syntax Error: Missing END statement!");
        }
    }

    static void ExecuteLine(string line)
    {
        if (line.StartsWith("PRINT "))
        {
            string content = line.Substring(6).Trim();
            object? result = EvaluateExpression(content);
            Console.WriteLine(result?.ToString() ?? "null");
        }
        else if (line.StartsWith("RETURN "))
        {
            string content = line.Substring(7).Trim();
            object? result = EvaluateExpression(content);
            throw new ReturnValueException(result);
        }
        else if (Regex.IsMatch(line, "^VAR [a-zA-Z_][a-zA-Z0-9_]* = .+$"))
        {
            string[] parts = line.Substring(4).Split(new[] { " = " }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                string varName = parts[0].Trim();
                string value = parts[1].Trim();

                if (keywords.Contains(varName))
                {
                    Console.WriteLine("Syntax Error: Cannot use a keyword as a variable name!");
                    return;
                }
                
                if (!IsValidIdentifier(varName))
                {
                    Console.WriteLine("Syntax Error: Invalid identifier name!");
                    return;
                }
                
                object? evaluatedValue = EvaluateExpression(value);
                variables[varName] = evaluatedValue ?? "null";
            }
        }
        else if (Regex.IsMatch(line, "^[a-zA-Z_][a-zA-Z0-9_]*\\s*[+\\-*/]=\\s*.+$")) // Handle compound assignment
        {
            var match = Regex.Match(line, "^([a-zA-Z_][a-zA-Z0-9_]*)\\s*([+\\-*/]=\\s*.+)$");
            if (match.Success)
            {
                string varName = match.Groups[1].Value;
                string operation = match.Groups[2].Value;
                
                if (!variables.ContainsKey(varName))
                {
                    Console.WriteLine("Syntax Error: Undefined variable " + varName);
                    return;
                }

                // Extract the operator and value
                var opMatch = Regex.Match(operation, "^([+\\-*/]=\\s*)(.+)$");
                if (opMatch.Success)
                {
                    string op = opMatch.Groups[1].Value.Trim();
                    string value = opMatch.Groups[2].Value.Trim();
                    
                    var currentValue = variables[varName];
                    var newValue = EvaluateExpression(value);
                    var result = op switch
                    {
                        "+=" => Add(currentValue, newValue),
                        "-=" => Subtract(currentValue, newValue),
                        "*=" => Multiply(currentValue, newValue),
                        "/=" => Divide(currentValue, newValue),
                        _ => null
                    };
                    
                    if (result is string str && str.StartsWith("Syntax Error:"))
                    {
                        Console.WriteLine(str);
                        return;
                    }
                    variables[varName] = result ?? "null";
                }
            }
        }
        else if (Regex.IsMatch(line, "^[a-zA-Z_][a-zA-Z0-9_]* = .+$")) // Handle regular assignment
        {
            string[] parts = line.Split(new[] { " = " }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                string varName = parts[0].Trim();
                string value = parts[1].Trim();
                
                if (!variables.ContainsKey(varName))
                {
                    Console.WriteLine("Syntax Error: Undefined variable " + varName);
                    return;
                }
                
                object? evaluatedValue = EvaluateExpression(value);
                if (evaluatedValue is string str && str.StartsWith("Syntax Error:"))
                {
                    Console.WriteLine(str);
                    return;
                }
                variables[varName] = evaluatedValue ?? "null";
            }
        }
        else if (functions.ContainsKey(line))
        {
            var (parameters, body) = functions[line];
            if (parameters.Count > 0)
            {
                Console.WriteLine("Syntax Error: Function requires parameters!");
                return;
            }
            foreach (string funcLine in body)
            {
                ExecuteLine(funcLine);
            }
        }
        else if (Regex.IsMatch(line, "^[a-zA-Z_][a-zA-Z0-9_]*\\((.*)\\)$")) // Function call with parameters
        {
            var match = Regex.Match(line, "^([a-zA-Z_][a-zA-Z0-9_]*)\\((.*)\\)$");
            string funcName = match.Groups[1].Value;
            string argsStr = match.Groups[2].Value;
            
            List<string> args = argsStr.Split(',')
                .Select(a => a.Trim())
                .Where(a => !string.IsNullOrEmpty(a))
                .ToList();

            object? result = ExecuteFunction(funcName, args);
            if (result != null)
            {
                Console.WriteLine(result);
            }
        }
        else if (Regex.IsMatch(line, "^[a-zA-Z_][a-zA-Z0-9_]* = [a-zA-Z_][a-zA-Z0-9_]*\\((.*)\\)$")) // Function call with assignment
        {
            var match = Regex.Match(line, "^([a-zA-Z_][a-zA-Z0-9_]*)\\s*=\\s*([a-zA-Z_][a-zA-Z0-9_]*)\\((.*)\\)$");
            string varName = match.Groups[1].Value;
            string funcName = match.Groups[2].Value;
            string argsStr = match.Groups[3].Value;
            
            List<string> args = argsStr.Split(',')
                .Select(a => a.Trim())
                .Where(a => !string.IsNullOrEmpty(a))
                .ToList();

            object? result = ExecuteFunction(funcName, args);
            if (result != null)
            {
                variables[varName] = result;
            }
        }
        else
        {
            Console.WriteLine("Syntax Error: Unrecognized statement!");
        }
    }

    static void ExecuteWhileLoop(List<string> loopBody)
    {
        if (loopBody.Count == 0) return;

        string condition = loopBody[0].Substring(6).Trim();
        List<string> body = loopBody.GetRange(1, loopBody.Count - 1);

        while (EvaluateExpression(condition) is bool result && result)
        {
            foreach (string line in body)
            {
                ExecuteLine(line);
            }
        }
    }

    static bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return false;
        if (keywords.Contains(identifier)) return false;
        if (!Regex.IsMatch(identifier, "^[a-zA-Z][a-zA-Z0-9_]*$")) return false;
        return true;
    }

    static object? EvaluateExpression(string expression)
    {
        try
        {
            // Handle variable references
            if (variables.ContainsKey(expression))
            {
                return variables[expression];
            }
            
            // Handle string literals with escape sequences
            if (Regex.IsMatch(expression, "^'(.*?)'$") || Regex.IsMatch(expression, "^\"(.*?)\"$"))
            {
                string content = expression.Substring(1, expression.Length - 2);
                return content.Replace("\\n", "\n")
                            .Replace("\\t", "\t")
                            .Replace("\\\"", "\"")
                            .Replace("\\'", "'")
                            .Replace("\\\\", "\\");
            }

            // Handle numeric literals
            if (Regex.IsMatch(expression, "^\\d+$")) return int.Parse(expression);
            if (Regex.IsMatch(expression, "^\\d+\\.\\d+$")) return float.Parse(expression);

            // Handle boolean literals
            if (expression == "true" || expression == "false") return bool.Parse(expression);

            // Handle null/undefined literals
            if (expression == "null" || expression == "undefined") return "null";
            
            // Handle string concatenation
            if (expression.Contains("+"))
            {
                var parts = expression.Split(new[] { "+" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    var results = parts.Select(p => EvaluateExpression(p.Trim())).ToList();
                    if (results.Any(r => r is string))
                    {
                        return string.Join("", results.Select(r => r?.ToString() ?? "null"));
                    }
                }
            }

            // Handle compound expressions
            foreach (var variable in variables)
            {
                expression = Regex.Replace(expression, $"\\b{variable.Key}\\b", variable.Value?.ToString() ?? "null");
            }

            // Handle compound assignment operators
            foreach (var op in new[] { "+=", "-=", "*=", "/=" })
            {
                if (expression.Contains(op))
                {
                    var parts = expression.Split(new[] { op }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var varName = parts[0].Trim();
                        var value = parts[1].Trim();
                        if (!variables.ContainsKey(varName))
                        {
                            return "Syntax Error: Undefined variable " + varName;
                        }
                        var currentValue = variables[varName];
                        var newValue = EvaluateExpression(value);
                        var result = op switch
                        {
                            "+=" => Add(currentValue, newValue),
                            "-=" => Subtract(currentValue, newValue),
                            "*=" => Multiply(currentValue, newValue),
                            "/=" => Divide(currentValue, newValue),
                            _ => null
                        };
                        if (result is string str && str.StartsWith("Syntax Error:"))
                        {
                            return str;
                        }
                        variables[varName] = result ?? "null";
                        return result;
                    }
                }
            }

            // Handle logical operators
            if (expression.Contains("&&"))
            {
                var parts = expression.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Select(p => EvaluateExpression(p.Trim())).All(v => v is bool b && b);
            }
            if (expression.Contains("||"))
            {
                var parts = expression.Split(new[] { "||" }, StringSplitOptions.None);
                return parts.Select(p => EvaluateExpression(p.Trim())).Any(v => v is bool b && b);
            }
            if (expression.StartsWith("!"))
            {
                var value = EvaluateExpression(expression.Substring(1).Trim());
                return value is bool b ? !b : "Syntax Error: Invalid boolean expression!";
            }

            // Handle comparison operators
            foreach (var op in new[] { "==", "!=", "<", ">", "<=", ">=" })
            {
                if (expression.Contains(op))
                {
                    var parts = expression.Split(new[] { op }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var left = EvaluateExpression(parts[0].Trim());
                        var right = EvaluateExpression(parts[1].Trim());
                        return op switch
                        {
                            "==" => left?.ToString() == right?.ToString(),
                            "!=" => left?.ToString() != right?.ToString(),
                            "<" => Compare(left, right) < 0,
                            ">" => Compare(left, right) > 0,
                            "<=" => Compare(left, right) <= 0,
                            ">=" => Compare(left, right) >= 0,
                            _ => null
                        };
                    }
                }
            }

            // Handle arithmetic operators
            DataTable dt = new DataTable();
            return dt.Compute(expression, "");
        }
        catch
        {
            return "Syntax Error: Invalid Expression!";
        }
    }

    static int Compare(object? a, object? b)
    {
        if (a is int ai && b is int bi) return ai.CompareTo(bi);
        if (a is float af && b is float bf) return af.CompareTo(bf);
        if (a is string as_ && b is string bs) return as_.CompareTo(bs);
        return 0;
    }

    static object? Add(object? a, object? b)
    {
        if (a is int ai && b is int bi) return ai + bi;
        if (a is float af && b is float bf) return af + bf;
        if (a is string as_ && b is string bs) return as_ + bs;
        return "Syntax Error: Invalid operands for addition!";
    }

    static object? Subtract(object? a, object? b)
    {
        if (a is int ai && b is int bi) return ai - bi;
        if (a is float af && b is float bf) return af - bf;
        return "Syntax Error: Invalid operands for subtraction!";
    }

    static object? Multiply(object? a, object? b)
    {
        if (a is int ai && b is int bi) return ai * bi;
        if (a is float af && b is float bf) return af * bf;
        return "Syntax Error: Invalid operands for multiplication!";
    }

    static object? Divide(object? a, object? b)
    {
        if (a is int ai && b is int bi) return bi != 0 ? (float)ai / bi : "Syntax Error: Division by zero!";
        if (a is float af && b is float bf) return bf != 0 ? af / bf : "Syntax Error: Division by zero!";
        return "Syntax Error: Invalid operands for division!";
    }

    static object? ExecuteFunction(string funcName, List<string> args)
    {
        if (!functions.ContainsKey(funcName))
        {
            Console.WriteLine("Syntax Error: Undefined function " + funcName);
            return null;
        }

        var (parameters, body) = functions[funcName];
        if (args.Count != parameters.Count)
        {
            Console.WriteLine($"Syntax Error: Function {funcName} expects {parameters.Count} parameters, got {args.Count}");
            return null;
        }

        // Store current variable values
        var oldVars = new Dictionary<string, object>(variables);
        
        // Set parameter values
        for (int i = 0; i < parameters.Count; i++)
        {
            object? argValue = EvaluateExpression(args[i]);
            variables[parameters[i]] = argValue ?? "null";
        }

        try
        {
            // Execute function body
            foreach (string funcLine in body)
            {
                if (funcLine.StartsWith("FUNC "))
                {
                    // Skip the function definition line
                    continue;
                }
                if (funcLine.StartsWith("RETURN "))
                {
                    string content = funcLine.Substring(7).Trim();
                    object? result = EvaluateExpression(content);
                    return result;
                }
                if (funcLine == "BEGIN" || funcLine == "END")
                {
                    continue;
                }
                ExecuteLine(funcLine);
            }
            return null;
        }
        catch (ReturnValueException ex)
        {
            return ex.Value;
        }
        finally
        {
            // Restore variable values
            variables = oldVars;
        }
    }

    class ReturnValueException : Exception
    {
        public object? Value { get; }
        public ReturnValueException(object? value) : base()
        {
            Value = value;
        }
    }
}
