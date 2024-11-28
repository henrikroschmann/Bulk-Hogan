using System.Globalization;
using System.Linq.Expressions;
using System.Text;

namespace BulkHogan.Extensions;

public static class ExpressionExtensions
{
    public static string ConvertConditionToSQL<T>(this Expression<Func<T, T, bool>> expression)
    {
        var visitor = new MergeConditionVisitor();
        visitor.Visit(expression);
        return visitor.Condition;
    }

    private sealed class MergeConditionVisitor : ExpressionVisitor
    {
        private readonly StringBuilder _sb = new();
        public string Condition => _sb.ToString();

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _sb.Append('(');
            Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    _sb.Append(" = ");
                    break;

                case ExpressionType.NotEqual:
                    _sb.Append(" <> ");
                    break;

                case ExpressionType.GreaterThan:
                    _sb.Append(" > ");
                    break;

                case ExpressionType.GreaterThanOrEqual:
                    _sb.Append(" >= ");
                    break;

                case ExpressionType.LessThan:
                    _sb.Append(" < ");
                    break;

                case ExpressionType.LessThanOrEqual:
                    _sb.Append(" <= ");
                    break;

                case ExpressionType.AndAlso:
                    _sb.Append(" AND ");
                    break;

                case ExpressionType.OrElse:
                    _sb.Append(" OR ");
                    break;

                default:
                    throw new NotSupportedException($"Operation '{node.NodeType}' is not supported");
            }

            Visit(node.Right);
            _sb.Append(')');
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression parameter)
            {
                string alias = parameter.Name == "existing" ? "target" : "EXCLUDED";
                _sb.Append(CultureInfo.CurrentCulture, $"{alias}.\"{node.Member.Name}\"");
                return node;
            }

            throw new NotSupportedException($"The member '{node.Member.Name}' is not supported");
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type == typeof(string))
            {
                _sb.Append(CultureInfo.CurrentCulture, $"'{node.Value}'");
            }
            else
            {
                _sb.Append(node.Value);
            }
            return node;
        }
    }
}
