using Microsoft.Z3;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Policy;
using MiaPlaza.ExpressionUtils;
using MiaPlaza.ExpressionUtils.Evaluating;

namespace Z3.LinqBinding
{

    public enum Optimization
    {
        Maximize,
        Minimize
    }

    public enum CollectionHandling
    {
        Constants,
        Array
    }


    public class Environment
    {
        public Expr Expr { get; set; }
        public Dictionary<PropertyInfo, Environment> Properties { get; set; } = new Dictionary<PropertyInfo, Environment>();
        public Boolean IsArray { get; set; }

    }

    public class MultipleEnvironment : Environment
    {
        public MultipleEnvironment(string prefix, Type elementType)
        {
            Prefix = prefix;
            ElementType = elementType;
        }
        public string Prefix { get; set; }
        public Type ElementType { get; set; }
        public Dictionary<object, Environment> SubEnvironments { get; set; } = new Dictionary<object, Environment>();

    }


    /// <summary>
    /// Representation of a theorem with its constraints.
    /// </summary>
    public class Theorem
    {

        public CollectionHandling DefaultCollectionHandling { get; set; } = CollectionHandling.Constants;
        public bool SimplifyLambdas { get; set; } = true;

        /// <summary>
        /// Theorem constraints.
        /// </summary>
        private IList<LambdaExpression> _constraints;

        /// <summary>
        /// Z3 context under which the theorem is solved.
        /// </summary>
        private Z3Context _context;

        /// <summary>
        /// Creates a new theorem for the given Z3 context.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        protected Theorem(Z3Context context)
            : this(context, new List<LambdaExpression>())
        {
        }

        /// <summary>
        /// Creates a new pre-constrained theorem for the given Z3 context.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        /// <param name="constraints">Constraints to apply to the created theorem.</param>
        protected Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints)
        {
            _context = context;
            _constraints = new List<LambdaExpression>(constraints);
        }

        /// <summary>
        /// Gets the constraints of the theorem.
        /// </summary>
        protected IEnumerable<LambdaExpression> Constraints
        {
            get
            {
                return _constraints;
            }
        }

        /// <summary>
        /// Gets the Z3 context under which the theorem is solved.
        /// </summary>
        protected Z3Context Context
        {
            get
            {
                return _context;
            }
        }

        /// <summary>
        /// Returns a comma-separated representation of the constraints embodied in the theorem.
        /// </summary>
        /// <returns>Comma-separated string representation of the theorem's constraints.</returns>
        public override string ToString()
        {
            //return string.Join(", ", (from c in _constraints select c.Body.ToString()).ToArray());
            return "";
        }


        public void AssertConstraint(LambdaExpression constraint)
        {
            this._constraints.Add(constraint);
        }



        /// <summary>
        /// Solves the theorem using Z3.
        /// </summary>
        /// <typeparam name="T">Theorem environment type.</typeparam>
        /// <returns>Result of solving the theorem; default(T) if the theorem cannot be satisfied.</returns>
        protected T Solve<T>()
        {
            // TODO: some debugging around issues with proper disposal of native resources…
            // using (Context context = _context.CreateContext())
            Context context = _context.CreateContext();
            {
                //var environment = GetEnvironment<T>(context);
                var environment = GetEnvironment(context, typeof(T));



                //Solver solver = context.MkSimpleSolver();
                Solver solver = context.MkSolver();


                AssertConstraints<T>(context, solver, environment);

                //Model model = null;
                //if (context.CheckAndGetModel(ref model) != LBool.True)
                //    return default(T);



                var sw = Stopwatch.StartNew();
                Status status = solver.Check();
                sw.Stop();
                _context.LogWriteLine($"Time to solution: {sw.Elapsed.TotalMilliseconds} ms");


                if (status != Status.SATISFIABLE)
                {
                    return default(T);
                }

                return GetSolution<T>(context, solver.Model, environment);
            }
        }

        /// <summary>
        /// Solves the theorem using Z3.
        /// </summary>
        /// <typeparam name="T">Theorem environment type.</typeparam>
        /// <returns>Result of solving the theorem; default(T) if the theorem cannot be satisfied.</returns>
        protected T Optimize<T>(Optimization direction, Expression<Func<T, int>> lambda)
        {

            Context context = _context.CreateContext();
            {
                //var environment = GetEnvironment<T>(context);
                var environment = GetEnvironment(context, typeof(T));



                Optimize optimizer = context.MkOptimize();


                AssertConstraints<T>(context, optimizer, environment);


                var exp = Visit(context, environment, lambda.Body, lambda.Parameters[0]);
                switch (direction)
                {
                    case Optimization.Maximize:
                        optimizer.MkMaximize(exp);
                        break;
                    case Optimization.Minimize:
                        optimizer.MkMinimize(exp);
                        break;
                }

                var sw = Stopwatch.StartNew();
                Status status = optimizer.Check();
                sw.Stop();
                _context.LogWriteLine($"Time to solution: {sw.Elapsed.TotalMilliseconds} ms");

                if (status != Status.SATISFIABLE)
                {
                    return default(T);
                }

                return GetSolution<T>(context, optimizer.Model, environment);
            }
        }





        private Environment GetEnvironment(Context context, Type targetType)
        {
            return GetEnvironment(context, targetType, targetType.Name, false);
        }

        private Environment GetEnvironment(Context context, Type targetType, string prefix, bool isArray)
        {
            var toReturn = new Environment();
            if (isArray || targetType.IsArray || (targetType.IsGenericType && typeof(ICollection).IsAssignableFrom(targetType.GetGenericTypeDefinition())))
            {
                Type elType;
                if (targetType.IsArray)
                {
                    elType = targetType.GetElementType();
                }
                else
                {
                    elType = targetType.GetGenericArguments()[0];
                }
                switch (DefaultCollectionHandling)
                {
                    case CollectionHandling.Constants:
                        return new MultipleEnvironment(prefix, elType);
                    case CollectionHandling.Array:
                        Expr constrExp = null;
                        Sort arrDomain;
                        Sort arrRange;
                        switch (Type.GetTypeCode(elType))
                        {
                            case TypeCode.String:
                                arrDomain = context.StringSort;
                                arrRange = context.StringSort;
                                //arrRange = context.MkBitVecSort(16);
                                break;
                            case TypeCode.Int16:
                                arrDomain = context.IntSort;
                                arrRange = context.IntSort;
                                //arrRange = context.MkBitVecSort(16);
                                break;
                            case TypeCode.Int32:
                                arrDomain = context.IntSort;
                                arrRange = context.IntSort;
                                break;
                            case TypeCode.Int64:
                            case TypeCode.DateTime:
                                arrDomain = context.IntSort;
                                arrRange = context.IntSort;
                                //arrRange = context.MkBitVecSort(64);
                                break;
                            case TypeCode.Boolean:
                                arrDomain = context.BoolSort;
                                arrRange = context.BoolSort;
                                break;
                            //case TypeCode.Single:
                            //    arrDomain = context.RealSort;
                            //    arrRange = context.MkFPSortSingle();
                            //    break;
                            //case TypeCode.Decimal:
                            //    arrDomain = context.RealSort;
                            //    arrRange = context.MkFPSortSingle();
                            //    break;
                            //case TypeCode.Double:
                            //    arrDomain = context.RealSort;
                            //    arrRange = context.MkFPSortDouble();
                            //    break;
                            case TypeCode.Single:
                            case TypeCode.Decimal:
                            case TypeCode.Double:
                                arrDomain = context.RealSort;
                                arrRange = context.RealSort;
                                break;
                            case TypeCode.Object:
                                toReturn.IsArray = true;
                                CreateEnvironmentProperties(toReturn, context, elType, prefix, true);
                                return toReturn;
                            default:
                                throw new NotSupportedException($"Unsupported member type {targetType.FullName}");

                        }
                        constrExp = context.MkArrayConst(prefix, arrDomain, arrRange);
                        toReturn.Expr = constrExp;
                        break;
                }

            }
            else
            {
                Expr constrExp;
                switch (Type.GetTypeCode(targetType))
                {
                    case TypeCode.String:
                        constrExp = context.MkConst(prefix, context.StringSort);
                        break;
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.DateTime:
                        constrExp = context.MkIntConst(prefix);
                        break;
                    case TypeCode.Boolean:
                        constrExp = context.MkBoolConst(prefix);
                        break;
                    case TypeCode.Single:
                    case TypeCode.Decimal:
                    case TypeCode.Double:
                        constrExp = context.MkRealConst(prefix);
                        break;
                    case TypeCode.Object:
                        CreateEnvironmentProperties(toReturn, context, targetType, prefix, false);
                        return toReturn;
                    default:
                        throw new NotSupportedException($"Unsupported parameter type for prefix {prefix} and target type {targetType}");
                }

                toReturn.Expr = constrExp;
            }

            return toReturn;
        }

        private void CreateEnvironmentProperties(Environment toUpdate, Context context, Type targetType, string prefix, bool isArray)
        {
            foreach (var parameter in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var parameterType = parameter.PropertyType;
                var parameterTypeMapping = (TheoremVariableTypeMappingAttribute)parameterType.GetCustomAttributes(typeof(TheoremVariableTypeMappingAttribute), false).SingleOrDefault();
                if (parameterTypeMapping != null)
                    parameterType = parameterTypeMapping.RegularType;
                var newPrefix = parameter.Name;
                if (!string.IsNullOrEmpty(prefix))
                {
                    newPrefix = $"{prefix}_{newPrefix}";
                }
                toUpdate.Properties[parameter] = GetEnvironment(context, parameterType, newPrefix, isArray);
            }
        }






        /// <summary>
        /// Asserts the theorem constraints on the Z3 context.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <typeparam name="T">Theorem environment type.</typeparam>
        private void AssertConstraints<T>(Context context, Object solverOrOptimizer, Environment environment)
        {
            var constraints = _constraints;

            //
            // Global rewriter registered?
            //
            var rewriterAttr = (TheoremGlobalRewriterAttribute)typeof(T).GetCustomAttributes(typeof(TheoremGlobalRewriterAttribute), false).SingleOrDefault();
            if (rewriterAttr != null)
            {
                //
                // Make sure the specified rewriter type implements the ITheoremGlobalRewriter.
                //
                var rewriterType = rewriterAttr.RewriterType;
                if (!typeof(ITheoremGlobalRewriter).IsAssignableFrom(rewriterType))
                    throw new InvalidOperationException("Invalid global rewriter type definition. Did you implement ITheoremGlobalRewriter?");

                //
                // Assume a parameterless public constructor to new up the rewriter.
                //
                var rewriter = (ITheoremGlobalRewriter)Activator.CreateInstance(rewriterType);

                //
                // Do the rewrite.
                //
                constraints = new List<LambdaExpression>(rewriter.Rewrite(constraints));
            }

            //
            // Visit, assert and log.
            //
            foreach (var constraint in constraints)
            {

                var flattenedBody = constraint.Body;
                if (SimplifyLambdas)
                {
                    flattenedBody = PartialEvaluator.PartialEvalBody(constraint, ExpressionInterpreter.Instance).Body;
                }
                //BoolExpr c = (BoolExpr)Visit(context, environment, constraint.Body, constraint.Parameters[0]);
                BoolExpr c = (BoolExpr)Visit(context, environment, flattenedBody, constraint.Parameters[0]);

                //context.AssertCnstr(c);
                if (solverOrOptimizer is Solver solver)
                {
                    solver.Assert(c);
                }
                else
                {
                    if (solverOrOptimizer is Optimize optimizer)
                    {
                        optimizer.Assert(c);
                    }
                }


                //_context.LogWriteLine(c.ToString());
            }
        }

        /// <summary>
        /// Gets the solution object for the solved theorem.
        /// </summary>
        /// <typeparam name="T">Environment type to create an instance of.</typeparam>
        /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <returns>Instance of the enviroment type with theorem-satisfying values.</returns>
        private T GetSolution<T>(Context context, Model model, Environment environment)
        {
            Type t = typeof(T);
            return (T)GetSolution(t, context, model, environment);
        }


        /// <summary>
        /// Gets the solution object for the solved theorem.
        /// </summary>
        /// <typeparam name="T">Environment type to create an instance of.</typeparam>
        /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <returns>Instance of the enviroment type with theorem-satisfying values.</returns>
        private object GetSolution(Type t, Context context, Model model, Environment environment)
        {


            //
            // Determine whether T is a compiler-generated type, indicating an anonymous type.
            // This check might not be reliable enough but works for now.
            //
            if (t.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any())
            {
                //
                // Anonymous types have a constructor that takes in values for all its properties.
                // However, we don't know the order and it's hard to correlate back the parameters
                // to the underlying properties. So, we want to bypass that constructor altogether
                // by using the FormatterServices to create an uninitialized (all-zero) instance.
                //
                object result = FormatterServices.GetUninitializedObject(t);

                //
                // Here we take advantage of undesirable knowledge on how anonymous types are
                // implemented by the C# compiler. This is risky but we can live with it for
                // now in this POC. Because the properties are get-only, we need to perform
                // nominal matching with the corresponding backing fields.
                //
                var fields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var parameter in environment.Properties.Keys)
                {
                    //
                    // Mapping from property to field.
                    //
                    var field = fields.SingleOrDefault(f => f.Name.StartsWith($"<{parameter.Name}>"));

                    //
                    // Evaluation of the values though the handle in the environment bindings.
                    //
                    var subEnv = environment.Properties[parameter];

                    Expr val = model.Eval(subEnv.Expr);
                    if (parameter.PropertyType == typeof(bool))
                        field.SetValue(result, val.IsTrue);
                    else if (parameter.PropertyType == typeof(int))
                        field.SetValue(result, ((IntNum)val).Int);
                    else
                        throw new NotSupportedException("Unsupported parameter type for " + parameter.Name + ".");
                }

                return result;
            }
            else
            {
                //
                // Straightforward case of having an "onymous type" at hand.
                //
                object result = Activator.CreateInstance(t);

                foreach (var parameter in environment.Properties.Keys)
                {

                    //
                    // Evaluation of the values though the handle in the environment bindings.
                    //

                    object value;

                    var subEnv = environment.Properties[parameter];


                    value = ConvertZ3Expression(result, context, model, subEnv, parameter);

                    parameter.SetValue(result, value, null);
                }

                return result;
            }
        }

        private object ConvertZ3Expression(object destinationObject, Context context, Model model, Environment subEnv, PropertyInfo parameter)
        {
            object value;

            //
            // Normalize types when facing Z3. Theorem variable type mappings allow for strong
            // typing within the theorem, while underlying variable representations are Z3-
            // friendly types.
            //
            var parameterType = parameter.PropertyType;
            var parameterTypeMapping = (TheoremVariableTypeMappingAttribute)parameterType.GetCustomAttributes(typeof(TheoremVariableTypeMappingAttribute), false).SingleOrDefault();
            if (parameterTypeMapping != null)
                parameterType = parameterTypeMapping.RegularType;

            Expr val = null;
            if (subEnv.Expr != null)
            {
                val = model.Eval(subEnv.Expr);

            }

            if (val != null && Type.GetTypeCode(parameterType) != TypeCode.Object &&
                val.ASTKind == Z3_ast_kind.Z3_APP_AST)
            {
                //value = Convert.ChangeType(null, parameterType);
                value = null;
            }
            else
            {
                switch (Type.GetTypeCode(parameterType))
                {
                    case TypeCode.String:
                        value = val.String;
                        break;
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                        value = ((IntNum)val).Int;
                        break;
                    case TypeCode.Int64:
                        value = ((IntNum)val).Int64;
                        break;
                    case TypeCode.DateTime:
                        value = DateTime.FromFileTime(((IntNum)val).Int64);
                        break;
                    case TypeCode.Boolean:
                        value = val.IsTrue;
                        break;
                    case TypeCode.Single:
                        value = double.Parse(((RatNum)val).ToDecimalString(32), CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.Decimal:
                        value = decimal.Parse(((RatNum)val).ToDecimalString(128), CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.Double:
                        value = double.Parse(((RatNum)val).ToDecimalString(64), CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.Object:
                        if (parameterType.IsArray || (parameterType.IsGenericType && typeof(ICollection).IsAssignableFrom(parameterType.GetGenericTypeDefinition())))
                        {
	                        var existingMember = parameter.GetValue(destinationObject, null);
							value = ExtractCollection(existingMember, context, model, subEnv, parameter, parameterType);
                        }
                        else
                        {
                            value = GetSolution(parameterType, context, model, subEnv);
                        }
                        break;
                    default:
                        throw new NotSupportedException("Unsupported parameter type for " + parameter.Name + ".");
                }
            }



            //
            // If there was a type mapping, we need to convert back to the original type.
            // In that case we expect a constructor with the mapped type to be available.
            //
            if (parameterTypeMapping != null)
            {
                var ctor = parameter.PropertyType.GetConstructor(new Type[] { parameterType });
                if (ctor == null)
                    throw new InvalidOperationException("Could not construct an instance of the mapped type " + parameter.PropertyType.Name + ". No public constructor with parameter type " + parameterType + " found.");

                value = ctor.Invoke(new object[] { value });
            }


            return value;
        }

        private object ExtractCollection(object existingMember, Context context, Model model, Environment subEnv,
	        PropertyInfo parameter, Type parameterType)
        {
	        object value;
	        Type eltType;
	        if (parameterType.IsArray)
	        {
		        eltType = parameterType.GetElementType();
	        }
	        else
	        {
		        eltType = parameterType.GetGenericArguments()[0];
	        }

	        if (eltType == null)
	        {
		        throw new NotSupportedException("Unsupported untyped array parameter type for " + parameter.Name + ".");
	        }

	        var results = new ArrayList();

	        var arrVal = subEnv.Expr as ArrayExpr;
	        var multiEnv = subEnv as MultipleEnvironment;

	        //todo: deal with keys and length in a more robust way
	        var keyType = typeof(int);

	        
	        ArrayList existingCollection = null;
	        int length = 0;
	        if (existingMember != null)
	        {
		        existingCollection = new ArrayList((ICollection)existingMember);
		        length = existingCollection.Count;
	        }

	        if (multiEnv != null && multiEnv.SubEnvironments.Keys.Count > 0)
	        {
		        var maxBound = multiEnv.SubEnvironments.Keys.Max();
		        keyType = maxBound.GetType();
		        int intMaxBound = Convert.ToInt32(maxBound);
		        length = Math.Max(length, intMaxBound + 1);
	        }

	        for (int i = 0; i < length; i++)
	        {
		        Expr numValExpr = null;
		        Environment subSubEnv = null;
		        if (arrVal != null)
		        {
			        // we deal with an array
			        numValExpr = model.Eval(context.MkSelect(arrVal, context.MkInt(i)));
		        }
		        else
		        {
			        // we deal with a constant based collection
			        object key = i;
			        if (key.GetType() != keyType)
			        {
				        key = TypeDescriptor.GetConverter(keyType).ConvertFrom(key);
			        }

			        if (multiEnv.SubEnvironments.TryGetValue(key, out subSubEnv))
			        {
				        if (subSubEnv.Expr != null)
				        {
					        numValExpr = model.Eval(subSubEnv.Expr);
				        }
			        }
		        }


		        object numVal = null;

		        if (existingCollection != null && existingCollection.Count > i)
		        {
			        numVal = existingCollection[i];
		        }


		        if ((subSubEnv != null && Type.GetTypeCode(eltType) == TypeCode.Object)
		            || (numValExpr != null && numValExpr.ASTKind != Z3_ast_kind.Z3_APP_AST))
		        {
			        switch (Type.GetTypeCode(eltType))
			        {
				        case TypeCode.String:
					        numVal = numValExpr.String;
					        break;
				        case TypeCode.Int16:
				        case TypeCode.Int32:
					        numVal = ((IntNum)numValExpr).Int;
					        break;
				        case TypeCode.Int64:
					        numVal = ((IntNum)numValExpr).Int64;
					        break;
				        case TypeCode.DateTime:
					        numVal = DateTime.FromFileTime(((IntNum)numValExpr).Int64);
					        break;
				        case TypeCode.Boolean:
					        numVal = numValExpr.IsTrue;
					        break;
				        case TypeCode.Single:
					        numVal = Double.Parse(((RatNum)numValExpr).ToDecimalString(32),
						        CultureInfo.InvariantCulture);
					        break;
				        case TypeCode.Decimal:
					        numVal = Decimal.Parse(((RatNum)numValExpr).ToDecimalString(128),
						        CultureInfo.InvariantCulture);
					        break;
				        case TypeCode.Double:
					        numVal = Double.Parse(((RatNum)numValExpr).ToDecimalString(64),
						        CultureInfo.InvariantCulture);
					        break;
				        case TypeCode.Object:
					        if (subSubEnv != null)
					        {
						        if (eltType.IsArray || (eltType.IsGenericType && typeof(ICollection).IsAssignableFrom(eltType.GetGenericTypeDefinition())))
						        {
									var existingSubMember = numVal;
									numVal = ExtractCollection(existingSubMember, context, model, subSubEnv, parameter, eltType);
						        }
						        else
						        {
									numVal = GetSolution(eltType, context, model, subSubEnv);
								}
					        }

					        break;
				        default:
					        throw new NotSupportedException(
						        $"Unsupported array parameter type for {parameter.Name} and array element type {eltType.Name}.");
			        }
		        }

		        results.Add(numVal);
	        }

	        value = parameterType.IsArray
		        ? results.ToArray(eltType)
		        : Activator.CreateInstance(parameterType, results.ToArray(eltType));
	        return value;
        }


        /// <summary>
        /// Main visitor method to translate the LINQ expression tree into a Z3 expression handle.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <param name="expression">LINQ expression tree node to be translated.</param>
        /// <param name="param">Parameter used to express the constraint on.</param>
        /// <returns>Z3 expression handle.</returns>
        private Expr Visit(Context context, Environment environment, Expression expression, ParameterExpression param)
        {
            //
            // Largely table-driven mechanism, providing constructor lambdas to generic Visit*
            // methods, classified by type and arity.
            //
            switch (expression.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkAnd((BoolExpr)a, (BoolExpr)b));

                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkOr((BoolExpr)a, (BoolExpr)b));

                case ExpressionType.ExclusiveOr:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkXor((BoolExpr)a, (BoolExpr)b));

                case ExpressionType.Not:
                    return VisitUnary(context, environment, (UnaryExpression)expression, param, (ctx, a) => ctx.MkNot((BoolExpr)a));

                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                    return VisitUnary(context, environment, (UnaryExpression)expression, param, (ctx, a) => ctx.MkUnaryMinus((ArithExpr)a));

                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkAdd((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkSub((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkMul((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.Divide:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkDiv((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.Modulo:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkRem((IntExpr)a, (IntExpr)b));

                case ExpressionType.LessThan:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkLt((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.LessThanOrEqual:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkLe((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.GreaterThan:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkGt((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.GreaterThanOrEqual:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkGe((ArithExpr)a, (ArithExpr)b));

                case ExpressionType.Equal:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkEq(a, b));

                case ExpressionType.NotEqual:
                    return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkNot(ctx.MkEq(a, b)));

                case ExpressionType.MemberAccess:
                    return VisitMember(context, environment, (MemberExpression)expression, param);

                case ExpressionType.Constant:
                    return VisitConstant(context, (ConstantExpression)expression);

                case ExpressionType.Call:
                    return VisitCall(context, environment, (MethodCallExpression)expression, param);
                case ExpressionType.ArrayIndex:
                case ExpressionType.Index:
                    return VisitCollectionAccess(context, environment, expression, param);
                default:
                    throw new NotSupportedException("Unsupported expression node type encountered: " + expression.NodeType);
            }
        }


        private Expr VisitConstantValue(Context context, Object val)
        {
            switch (Type.GetTypeCode(val.GetType()))
            {
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return context.MkInt(Convert.ToInt64(val));
                case TypeCode.Boolean:
                    return context.MkBool((bool)val);
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return context.MkReal(Convert.ToString(val,CultureInfo.InvariantCulture));
                case TypeCode.DateTime:
                    return context.MkInt(((DateTime)val).ToFileTimeUtc());
                case TypeCode.String:
                    return context.MkString(Convert.ToString(val, CultureInfo.InvariantCulture));
                default:
                    throw new NotSupportedException($"Unsupported constant {val}");

            }
        }


        /// <summary>
        /// Visitor method to translate a constant expression.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        /// <param name="constant">Constant expression.</param>
        /// <returns>Z3 expression handle.</returns>
        private Expr VisitConstant(Context context, ConstantExpression constant)
        {
            //if (constant.Type == typeof(int))
            //    //return context.MkNumeral((int)constant.Value, context.MkIntType());
            //    return context.MkNumeral((int)constant.Value, context.IntSort);
            //else if (constant.Type == typeof(bool))
            //    return (bool)constant.Value ? context.MkTrue() : context.MkFalse();

            //throw new NotSupportedException("Unsupported constant type.");
            return VisitConstantValue(context, constant.Value);

        }

        /// <summary>
        /// Visitor method to translate a member expression.
        /// </summary>
        /// <param name="context">the Z3 context to manipulate</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <param name="member">Member expression.</param>
        /// <param name="param">Parameter used to express the constraint on.</param>
        /// <param name="childHierarchy"></param>
        /// <returns>Z3 expression handle.</returns>
        private Expr VisitMember(Context context, Environment environment, MemberExpression member, ParameterExpression param, List<Expression> childHierarchy = null)
        {
            //
            // E.g. Symbols l = ...;
            //      theorem.Where(s => l.X1)
            //                         ^^
            //

            if (childHierarchy == null)
            {
                childHierarchy = new List<Expression>();
            }
            childHierarchy.Add(member);
            switch (member.Expression.NodeType)
            {
                case ExpressionType.Parameter:
                    if (member.Expression != param)
                    {
                        throw new NotSupportedException($"Could not reduce expression {member}");
                    }
                    break;
                case ExpressionType.MemberAccess:
                    return VisitMember(context, environment, (MemberExpression)member.Expression, param, childHierarchy);
                case ExpressionType.ArrayIndex:
                case ExpressionType.Index:
                    return VisitCollectionAccess(context, environment, member.Expression, param, childHierarchy);
                case ExpressionType.Constant:
                    // We only ever get here if SimplifyLambda is set to false, otherwise partial evaluation does it earlier
                    var cExpression = (ConstantExpression)member.Expression;
                    var target = cExpression.Value;
                    for (int i = childHierarchy.Count - 1; i >= 0; i--)
                    {
                        target = EvalMember(((MemberExpression)childHierarchy[i]).Member, target);
                    }
                    if (target != null)
                    {
                        return VisitConstantValue(context, target);
                    }
                    throw new NotSupportedException($"Could not reduce expression {member}");
                case ExpressionType.Call:
                    var call = (MethodCallExpression)member.Expression;
                    if (call.Method.Name.StartsWith("get_"))
                    {
                        // Assuming it's an indexed property
                        string prop = call.Method.Name.Substring(4);
                        var propInfo = call.Method.DeclaringType.GetProperty(prop);
                        var callTarget = call.Object;
                        var args = call.Arguments;
                        var indexer = Expression.MakeIndex(callTarget, propInfo, args);
                        return VisitCollectionAccess(context, environment, indexer, param, childHierarchy);
                    }
                    throw new NotSupportedException($"Could not reduce expression {member}");
                default:
                    throw new NotSupportedException($"Could not reduce expression {member}");
            }

            //
            // Only members we allow currently are direct accesses to the theorem's variables
            // in the environment type. So we just try to find the mapping from the environment
            // bindings table.
            //

            //PropertyInfo property = null;
            PropertyInfo childProperty = null;
            Environment subEnv = environment;
            Environment subSubEnv = null;
            Expression indexExp;
            Object index = null;

            for (int i = childHierarchy.Count - 1; i >= 0; i--)
            {

                var childExp = childHierarchy[i];
                switch (childExp.NodeType)
                {
                    case ExpressionType.MemberAccess:

                        if ((childProperty = ((MemberExpression)childExp).Member as PropertyInfo) != null
                            && subEnv.Properties.TryGetValue(childProperty, out subSubEnv))
                        {
                            subEnv = subSubEnv;
                        }
                        else
                        {
                            // this is an index
                            indexExp = PartialEvaluator.PartialEval(childExp, ExpressionInterpreter.Instance);
                            if (indexExp.NodeType != ExpressionType.Constant)
                            {
                                throw new NotSupportedException($"Could not reduce expression {indexExp}");
                            }
                            index = ExpressionInterpreter.Instance.Interpret(indexExp);

                        }
                        break;
                    default:
                        // this is an index
                        indexExp = PartialEvaluator.PartialEval(childExp, ExpressionInterpreter.Instance);
                        if (indexExp.NodeType != ExpressionType.Constant)
                        {
                            throw new NotSupportedException($"Could not reduce expression {indexExp}");
                        }
                        index = ExpressionInterpreter.Instance.Interpret(indexExp);

                        break;
                }

                if (index != null)
                {
                    if (subEnv is MultipleEnvironment multiEnv)
                    {
                        if (!multiEnv.SubEnvironments.TryGetValue(index, out subSubEnv))
                        {
                            var newPrefix = $"{multiEnv.Prefix}_{index}";
                            subSubEnv = GetEnvironment(context, multiEnv.ElementType, newPrefix, false);
                            multiEnv.SubEnvironments[index] = subSubEnv;
                        }
                        subEnv = subSubEnv;
                        index = null;
                    }
                }

                if (subEnv.Expr != null)
                {
                    return subEnv.Expr;
                }
            }

            throw new NotSupportedException($"Could not reduce expression {member}");
        }


        private static Object EvalMember(MemberInfo member, object target)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Property:
                    return ((PropertyInfo)member).GetValue(target, null);
                case MemberTypes.Field:
                    return ((FieldInfo)member).GetValue(target);
                default:
                    //val = target = null;
                    throw new NotSupportedException($"Unsupported constant {target} .");
            }

        }



        /// <summary>
        /// Visitor method to translate a binary expression.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <param name="expression">Binary expression.</param>
        /// <param name="ctor">Constructor to combine recursive visitor results.</param>
        /// <param name="param">Parameter used to express the constraint on.</param>
        /// <returns>Z3 expression handle.</returns>
        private Expr VisitBinary(Context context, Environment environment, BinaryExpression expression, ParameterExpression param, Func<Context, Expr, Expr, Expr> ctor)
        {
            return ctor(context, Visit(context, environment, expression.Left, param), Visit(context, environment, expression.Right, param));
        }


        private Expr VisitCollectionAccess(Context context, Environment environment, Expression expression,
            ParameterExpression param, List<Expression> childHierarchy = null)
        {
            //var envType = environment.GetType();
            //if (envType.IsGenericType && )
            //{

            //}

            Expression targetExpression;
            IEnumerable<Expression> argumentExpressions;
            switch (expression.NodeType)
            {
                case ExpressionType.ArrayIndex:
                    var binaryExpression = ((BinaryExpression)expression);
                    targetExpression = binaryExpression.Left;
                    argumentExpressions = new Expression[] { binaryExpression.Right };
                    break;
                case ExpressionType.Index:
                    var indexExpression = ((IndexExpression)expression);
                    targetExpression = indexExpression.Object;
                    argumentExpressions = indexExpression.Arguments;
                    break;
                default:
                    throw new ArgumentException("Expression type not supported for collections", nameof(expression));
            }

            Expr target;
            if (targetExpression.NodeType == ExpressionType.MemberAccess || targetExpression.NodeType == ExpressionType.ArrayIndex)
            {
                if (childHierarchy == null)
                {
                    childHierarchy = new List<Expression>();
                }
                childHierarchy.AddRange(argumentExpressions);
                if (targetExpression.NodeType == ExpressionType.MemberAccess)
                {
	                target = VisitMember(context, environment, (MemberExpression)targetExpression, param, childHierarchy);
				}
                else
                {
					target = VisitCollectionAccess(context, environment, targetExpression, param, childHierarchy);
				}
				

                if (!(target is ArrayExpr))
                {
                    return target;
                }
            }
            else
            {
                target = Visit(context, environment, targetExpression, param);
            }

            var targetArrayExpr = (ArrayExpr)target;

            Expr[] args;
            switch (expression.NodeType)
            {
                case ExpressionType.ArrayIndex:
                    var binaryExpression = ((BinaryExpression)expression);
                    args = new Expr[] { Visit(context, environment, binaryExpression.Right, param) };
                    break;
                case ExpressionType.Index:
                    var indexExpression = ((IndexExpression)expression);
                    args = indexExpression.Arguments.Select(argExp => Visit(context, environment, argExp, param)).ToArray();
                    break;
                default:
                    throw new ArgumentException("Expression type not supported for collections", nameof(expression));
            }
            return context.MkSelect(targetArrayExpr, args);

        }


        //private Expr VisitIndex(Context context, Environment environment, IndexExpression expression, ParameterExpression param, Func<Context, Expr, Expr[], Expr> ctor)
        //{
        //    var args = expression.Arguments.Select(argExp => Visit(context, environment, argExp, param)).ToArray();
        //    //return ctor(context, Visit(context, environment, expression.Object, param), Visit(context, environment, expression.Arguments[0], param));
        //    return ctor(context, Visit(context, environment, expression.Object, param), args);
        //}

        /// <summary>
        /// Visitor method to translate a method call expression.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <param name="call">Method call expression.</param>
        /// <param name="param">Parameter used to express the constraint on.</param>
        /// <returns>Z3 expression handle.</returns>
        private Expr VisitCall(Context context, Environment environment, MethodCallExpression call, ParameterExpression param)
        {
            var method = call.Method;

            //
            // Does the method have a rewriter attribute applied?
            //
            var rewriterAttr = (TheoremPredicateRewriterAttribute)method.GetCustomAttributes(typeof(TheoremPredicateRewriterAttribute), false).SingleOrDefault();
            if (rewriterAttr != null)
            {
                //
                // Make sure the specified rewriter type implements the ITheoremPredicateRewriter.
                //
                var rewriterType = rewriterAttr.RewriterType;
                if (!typeof(ITheoremPredicateRewriter).IsAssignableFrom(rewriterType))
                    throw new InvalidOperationException("Invalid predicate rewriter type definition. Did you implement ITheoremPredicateRewriter?");

                //
                // Assume a parameterless public constructor to new up the rewriter.
                //
                var rewriter = (ITheoremPredicateRewriter)Activator.CreateInstance(rewriterType);

                //
                // Make sure we don't get stuck when the rewriter just returned its input. Valid
                // rewriters should satisfy progress guarantees.
                //
                var result = rewriter.Rewrite(call);
                if (result == call)
                    throw new InvalidOperationException("The expression tree rewriter of type " + rewriterType.Name + " did not perform any rewrite. Aborting compilation to avoid infinite looping.");

                //
                // Visit the rewritten expression.
                //
                return Visit(context, environment, result, param);
            }

            //
            // Filter for known Z3 operators.
            //
            if (method.IsGenericMethod && method.GetGenericMethodDefinition() == typeof(Z3Methods).GetMethod("Distinct"))
            {
                //
                // We know the signature of the Distinct method call. Its argument is a params
                // array, hence we expect a NewArrayExpression.
                //
                var itemsExpression = call.Arguments[0];

                return VisitArrayMethod(context, environment, itemsExpression, param, (ctx, args) => ctx.MkDistinct(args.Select(arg => Visit(context, environment, arg, param)).ToArray()));
            }

            if (method.DeclaringType == typeof(Enumerable) && method.Name == nameof(Enumerable.Sum) && method.GetParameters().Length == 1)
            {
                var itemsExpression = call.Arguments[0];

                return VisitArrayMethod(context, environment, itemsExpression, param, (ctx, args) =>
                    Visit(context, environment, Expression.Add(args[0], args.Length > 2 ? Expression.Call(method, Expression.NewArrayInit(args[0].Type, args.Skip(1).ToArray())) : args[1]), param));
            }

            if (method.Name.StartsWith("get_"))
            {
                // Assuming it's an indexed property
                string prop = method.Name.Substring(4);
                var propinfo = method.DeclaringType.GetProperty(prop);
                var target = call.Object;
                var args = call.Arguments;
                var indexer = Expression.MakeIndex(target, propinfo, args);
                return Visit(context, environment, indexer, param);

            }

            throw new NotSupportedException("Unknown method call:" + method.ToString());
        }


        private Expr VisitArrayMethod(Context context, Environment environment, Expression itemsExpression, ParameterExpression param, Func<Context, Expression[], Expr> ctor)
        {
            IEnumerable<Expression> arrExpressions = null;

            if (itemsExpression is MethodCallExpression mExp)
            {
                arrExpressions = MethodCallToArray(mExp);
            }
            else
            {
                if (itemsExpression is NewArrayExpression arrExp)
                {
                    arrExpressions = arrExp.Expressions;
                }
            }

            if (arrExpressions == null)
            {
                throw new NotSupportedException("unsuported Expression :" + itemsExpression.ToString());

            }


            //var args = from arg in arrExpressions select Visit(context, environment, arg, param);
            return ctor(context, arrExpressions.ToArray());
        }


        private IEnumerable<Expression> MethodCallToArray(MethodCallExpression mExp)
        {
            if (mExp.Method.IsGenericMethod && mExp.Method.GetGenericMethodDefinition() == typeof(Enumerable)
                    .GetMethods().First(m => m.Name == nameof(Enumerable.ToArray)))
            {
                var callerToArrayExp = mExp.Arguments[0];
                if (callerToArrayExp is MethodCallExpression callerToArrayMethodExp)
                {
                    return MethodCallToArray(callerToArrayMethodExp);
                }
                else
                {
                    throw new NotSupportedException($"unsuported Expression {mExp}, ToArray is only supported after a select");
                }
            }
            if (mExp.Method.IsGenericMethod
                       && mExp.Method.GetGenericMethodDefinition() == typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Select)
                                                                                                                               && m.GetParameters().Length == 2))
            {
                var callerExpression = mExp.Arguments[0];
                var caller = new ArrayList();
                if (callerExpression is MethodCallExpression mSubExp)
                {
                    var subExpressions = MethodCallToArray(mSubExp);
                    foreach (var subExpression in subExpressions)
                    {
                        var interpreted = (IEnumerable)ExpressionInterpreter.Instance.Interpret(callerExpression);
                        foreach (var item in interpreted)
                        {
                            caller.Add(item);
                        }
                    }
                }
                else
                {
                    var interpreted = (IEnumerable)ExpressionInterpreter.Instance.Interpret(callerExpression);
                    foreach (var item in interpreted)
                    {
                        caller.Add(item);
                    }
                }

                //var arg = PartialEvaluator.PartialEval(call.Arguments[1], ExpressionInterpreter.Instance) as LambdaExpression;
                var arg = mExp.Arguments[1] as LambdaExpression;
                var subExps = new List<Expression>(caller.Count);
                foreach (var item in caller)
                {
                    var substitutedExpression =
                        ParameterSubstituter.SubstituteParameter(arg, Expression.Constant(item));
                    var newlyFlattened = PartialEvaluator.PartialEval(substitutedExpression, ExpressionInterpreter.Instance);
                    if (newlyFlattened is MethodCallExpression subMethodCallExpression
                        && typeof(IEnumerable).IsAssignableFrom(subMethodCallExpression.Method.ReturnType))
                    {
                        subExps.AddRange(MethodCallToArray(subMethodCallExpression));
                    }
                    else
                    {
                        subExps.Add(newlyFlattened);
                    }
                }

                return subExps;
            }

            throw new NotSupportedException("unsuported method call:" + mExp.Method.Name);
        }


        /// <summary>
        /// Visitor method to translate a unary expression.
        /// </summary>
        /// <param name="context">Z3 context.</param>
        /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
        /// <param name="expression">Unary expression.</param>
        /// <param name="ctor">Constructor to combine recursive visitor results.</param>
        /// <param name="param">Parameter used to express the constraint on.</param>
        /// <returns>Z3 expression handle.</returns>
        private Expr VisitUnary(Context context, Environment environment, UnaryExpression expression, ParameterExpression param, Func<Context, Expr, Expr> ctor)
        {
            return ctor(context, Visit(context, environment, expression.Operand, param));
        }

    }
}