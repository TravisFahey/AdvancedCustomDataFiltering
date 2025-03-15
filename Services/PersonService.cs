using AdvancedCustomDataFiltering.Data;
using AdvancedCustomDataFiltering.Models;
using System.Linq.Expressions;

namespace AdvancedCustomDataFiltering.Services
{
	public interface IPersonService
	{
		public DataListDTO GetPeople(ListRequestModel request);
	}

	public class PersonService : IPersonService
	{
		//define columns to search by
		public List<string> searchProperties = new()
		{
			"FirstName",
			"LastName"
		};

		//query with paging/filtering/sorting oblist
		public DataListDTO GetPeople(ListRequestModel request)
		{
			var dataRepo = new DataRepo();
			// Get the filtered or total count of records
			var query = dataRepo.People.AsQueryable();

			var visibleColumns = request.Columns.Where(x => x.IsVisible).ToList();

			if (!string.IsNullOrEmpty(request.Search))
			{
				var parameterExpression = Expression.Parameter(typeof(Person), "observation");
				Expression combinedExpression = null;


				// Build the search expression only for visible columns among the specified properties
				foreach (var propertyName in searchProperties)
				{
					var column = request.Columns.FirstOrDefault(c => c.PropertyName == propertyName);

					if (column != null && column.IsVisible)
					{
						var property = Expression.PropertyOrField(parameterExpression, propertyName);

						// Ensure the property is of type string to avoid runtime errors
						if (property.Type == typeof(string))
						{
							var method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
							var searchExpression = Expression.Constant(request.Search, typeof(string));
							var containsExpression = Expression.Call(property, method, searchExpression);

							if (combinedExpression == null)
							{
								combinedExpression = containsExpression;
							}
							else
							{
								combinedExpression = Expression.OrElse(combinedExpression, containsExpression);
							}
						}
					}
				}

				if (combinedExpression != null)
				{
					var lambda = Expression.Lambda<Func<Person, bool>>(combinedExpression, parameterExpression);
					query = query.Where(lambda);
				}
			}

			List<ColumnFilterDefinition> filters = new();

			foreach (var c in request.Columns)
			{
				foreach (var f in c.Filters)
				{
					f.Column = c.PropertyName;
					filters.Add(f);
				}
			}

			if (filters != null && filters.Any())
			{
				var parameterExpression = Expression.Parameter(typeof(Person), "observation");
				Expression finalExpression = null;

				// 1) Group filters by their 'Column' property
				var groupedFilters = filters
					.GroupBy(f => f.Column, StringComparer.OrdinalIgnoreCase);

				// 2) For each column group, build an OR/AND chain respecting filter.AndOr
				foreach (var group in groupedFilters)
				{
					Expression propertyExpression = null;

					foreach (var filter in group)
					{
						// Build individual filter expression
						var filterExpression = BuildFilterExpression(filter, parameterExpression);

						if (propertyExpression == null)
						{
							propertyExpression = filterExpression;
						}
						else
						{
							// Respect the filter's AndOr setting if needed:
							var andOr = filter.AndOr?.ToLower();

							// If your requirement is "multiple filters for the same property
							// should default to OR," then always use OR here. 
							// But if you want to use filter.AndOr, apply the logic below:
							if (andOr == "or")
							{
								propertyExpression = Expression.OrElse(propertyExpression, filterExpression);
							}
							else
							{
								propertyExpression = Expression.AndAlso(propertyExpression, filterExpression);
							}
						}
					}

					// 3) Combine this property's grouped expression with the final expression.
					// Usually, we want "AND" across different columns:
					if (finalExpression == null)
					{
						finalExpression = propertyExpression;
					}
					else
					{
						finalExpression = Expression.AndAlso(finalExpression, propertyExpression);
					}
				}

				// 4) Wrap and apply the final expression
				if (finalExpression != null)
				{
					var lambda = Expression.Lambda<Func<Person, bool>>(finalExpression, parameterExpression);
					query = query.Where(lambda);
				}
			}

			if (request.Columns != null && request.Columns.Any(c => !string.IsNullOrEmpty(c.SortDirection)))
			{
				query = ApplySorting(query, request.Columns);
			}
			else
			{
				// Default sorting
				query = query.OrderBy(o => o.FirstName)
					.ThenBy(o => o.LastName);
			}

			// Retrieve the paginated data
			var obs = query
				.Skip((request.Page - 1) * request.PageSize)
				.Take(request.PageSize)
				.ToList();

			var totalCount = query.Count();

			DataListDTO data = new DataListDTO()
			{
				Items = obs,
				ItemTotalCount = totalCount
			};

			return data;
		}

		private Expression BuildFilterExpression(ColumnFilterDefinition filter, ParameterExpression parameterExpression)
		{
			var propertyName = filter.Column;
			var propertyExpression = Expression.Property(parameterExpression, propertyName);
			var propertyType = typeof(Person).GetProperty(propertyName)?.PropertyType;

			if (propertyType == null)
			{
				throw new InvalidOperationException($"Property '{propertyName}' does not exist on type '{typeof(Person).Name}'.");
			}

			// Dynamic conversion of filter.Value based on the propertyType
			object convertedValue;
			Type nonNullablePropertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

			switch (Type.GetTypeCode(nonNullablePropertyType))
			{
				case TypeCode.Int32:
				case TypeCode.Int64:
					if (!int.TryParse(filter.Value, out int intValue))
					{
						throw new InvalidOperationException("The provided filter value is not a valid integer.");
					}
					convertedValue = intValue;
					break;
				case TypeCode.String:
					convertedValue = filter.Value;
					break;
				case TypeCode.DateTime:
					if (filter.DateTimeValue == null || filter.DateTimeValue.Value == null)
					{
						throw new InvalidOperationException("The provided filter value is not a valid DateTime.");
					}
					convertedValue = filter.DateTimeValue.Value.Date;
					break;
				case TypeCode.Boolean:
					if (!bool.TryParse(filter.Value, out bool boolValue))
					{
						throw new InvalidOperationException("The provided filter value is not a valid boolean.");
					}
					convertedValue = boolValue;
					break;
				case TypeCode.Double:
				case TypeCode.Decimal:
					if (!double.TryParse(filter.Value, out double doubleValue))
					{
						throw new InvalidOperationException("The provided filter value is not a valid number.");
					}
					convertedValue = doubleValue;
					break;
				default:
					throw new InvalidOperationException($"Unsupported property type: {propertyType}");
			}

			// Adjust propertyExpression and valueExpression for DateTime types
			Expression valueExpression = Expression.Constant(convertedValue, propertyType);
			if (nonNullablePropertyType == typeof(DateTime))
			{
				// Extract Date component from propertyExpression
				if (propertyType == typeof(DateTime?))
				{
					// Handle nullable DateTime
					var valueProperty = Expression.Property(propertyExpression, "Value");
					propertyExpression = Expression.Property(valueProperty, "Date");
					// Adjust valueExpression to be non-nullable DateTime
					valueExpression = Expression.Constant(convertedValue, typeof(DateTime));
				}
				else
				{
					propertyExpression = Expression.Property(propertyExpression, "Date");
					valueExpression = Expression.Constant(convertedValue, typeof(DateTime));
				}
			}

			Expression body;

			switch (filter.Operator!.ToLower())
			{
				case "contains":
					if (propertyType == typeof(string))
					{
						body = Expression.Call(propertyExpression, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, valueExpression);
					}
					else
					{
						throw new InvalidOperationException($"Operator 'Contains' cannot be applied to property of type '{propertyType.Name}'.");
					}
					break;

				case "not contains":
					if (propertyType == typeof(string))
					{
						body = Expression.Not(Expression.Call(propertyExpression, typeof(string).GetMethod("Contains", new[] { typeof(string) })!, valueExpression));
					}
					else
					{
						throw new InvalidOperationException($"Operator 'Not Contains' cannot be applied to property of type '{propertyType.Name}'.");
					}
					break;

				case "equals":
				case "=":
					if (nonNullablePropertyType == typeof(DateTime))
					{
						if (propertyType == typeof(DateTime?))
						{
							var hasValue = Expression.Property(Expression.Property(parameterExpression, propertyName), "HasValue");
							body = Expression.AndAlso(
								hasValue,
								Expression.Equal(propertyExpression, valueExpression)
							);
						}
						else
						{
							body = Expression.Equal(propertyExpression, valueExpression);
						}
					}
					else
					{
						body = Expression.Equal(propertyExpression, valueExpression);
					}
					break;

				case "not equals":
				case "!=":
					if (nonNullablePropertyType == typeof(DateTime))
					{
						if (propertyType == typeof(DateTime?))
						{
							var hasValue = Expression.Property(Expression.Property(parameterExpression, propertyName), "HasValue");
							body = Expression.OrElse(
								Expression.Not(hasValue),
								Expression.NotEqual(propertyExpression, valueExpression)
							);
						}
						else
						{
							body = Expression.NotEqual(propertyExpression, valueExpression);
						}
					}
					else
					{
						body = Expression.NotEqual(propertyExpression, valueExpression);
					}
					break;

				case "starts with":
					if (propertyType == typeof(string))
					{
						body = Expression.Call(propertyExpression, typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!, valueExpression);
					}
					else
					{
						throw new InvalidOperationException($"Operator 'Starts With' cannot be applied to property of type '{propertyType.Name}'.");
					}
					break;

				case "ends with":
					if (propertyType == typeof(string))
					{
						body = Expression.Call(propertyExpression, typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!, valueExpression);
					}
					else
					{
						throw new InvalidOperationException($"Operator 'Ends With' cannot be applied to property of type '{propertyType.Name}'.");
					}
					break;

				case "is empty":
					if (propertyType == typeof(string))
					{
						var nullCheck = Expression.Equal(propertyExpression, Expression.Constant(null, typeof(string)));
						var emptyCheck = Expression.Equal(propertyExpression, Expression.Constant(string.Empty));
						body = Expression.OrElse(nullCheck, emptyCheck);
					}
					else if (propertyType == typeof(int?) || propertyType == typeof(double?) || propertyType == typeof(bool?) || propertyType == typeof(DateTime?))
					{
						body = Expression.Equal(propertyExpression, Expression.Constant(null, propertyType));
					}
					else
					{
						throw new InvalidOperationException($"Operator 'Is Empty' cannot be applied to property of type '{propertyType.Name}'.");
					}
					break;

				case "is not empty":
					if (propertyType == typeof(string))
					{
						var nullCheck = Expression.NotEqual(propertyExpression, Expression.Constant(null, typeof(string)));
						var emptyCheck = Expression.NotEqual(propertyExpression, Expression.Constant(string.Empty));
						body = Expression.AndAlso(nullCheck, emptyCheck);
					}
					else if (propertyType == typeof(int?) || propertyType == typeof(double?) || propertyType == typeof(bool?) || propertyType == typeof(DateTime?))
					{
						body = Expression.NotEqual(propertyExpression, Expression.Constant(null, propertyType));
					}
					else
					{
						throw new InvalidOperationException($"Operator 'Is Not Empty' cannot be applied to property of type '{propertyType.Name}'.");
					}
					break;

				case ">=":
					if (nonNullablePropertyType == typeof(DateTime))
					{
						if (propertyType == typeof(DateTime?))
						{
							var hasValue = Expression.Property(Expression.Property(parameterExpression, propertyName), "HasValue");
							body = Expression.AndAlso(
								hasValue,
								Expression.GreaterThanOrEqual(propertyExpression, valueExpression)
							);
						}
						else
						{
							body = Expression.GreaterThanOrEqual(propertyExpression, valueExpression);
						}
					}
					else
					{
						body = Expression.GreaterThanOrEqual(propertyExpression, valueExpression);
					}
					break;

				case "<=":
					if (nonNullablePropertyType == typeof(DateTime))
					{
						if (propertyType == typeof(DateTime?))
						{
							var hasValue = Expression.Property(Expression.Property(parameterExpression, propertyName), "HasValue");
							body = Expression.AndAlso(
								hasValue,
								Expression.LessThanOrEqual(propertyExpression, valueExpression)
							);
						}
						else
						{
							body = Expression.LessThanOrEqual(propertyExpression, valueExpression);
						}
					}
					else
					{
						body = Expression.LessThanOrEqual(propertyExpression, valueExpression);
					}
					break;

				case ">":
					if (nonNullablePropertyType == typeof(DateTime))
					{
						if (propertyType == typeof(DateTime?))
						{
							var hasValue = Expression.Property(Expression.Property(parameterExpression, propertyName), "HasValue");
							body = Expression.AndAlso(
								hasValue,
								Expression.GreaterThan(propertyExpression, valueExpression)
							);
						}
						else
						{
							body = Expression.GreaterThan(propertyExpression, valueExpression);
						}
					}
					else
					{
						body = Expression.GreaterThan(propertyExpression, valueExpression);
					}
					break;

				case "<":
					if (nonNullablePropertyType == typeof(DateTime))
					{
						if (propertyType == typeof(DateTime?))
						{
							var hasValue = Expression.Property(Expression.Property(parameterExpression, propertyName), "HasValue");
							body = Expression.AndAlso(
								hasValue,
								Expression.LessThan(propertyExpression, valueExpression)
							);
						}
						else
						{
							body = Expression.LessThan(propertyExpression, valueExpression);
						}
					}
					else
					{
						body = Expression.LessThan(propertyExpression, valueExpression);
					}
					break;

				case "is":
					if (propertyType == typeof(bool) || propertyType == typeof(bool?))
					{
						body = Expression.Equal(propertyExpression, valueExpression);
					}
					else
					{
						throw new InvalidOperationException($"Operator 'Is' cannot be applied to property of type '{propertyType.Name}'.");
					}
					break;

				default:
					throw new NotSupportedException($"Operator '{filter.Operator}' is not supported.");
			}

			return body;
		}

		private static IOrderedQueryable<Person> ApplySorting(IQueryable<Person> query, List<ColumnDefinition> columns)
		{
			IOrderedQueryable<Person> orderedQuery = null;
			var parameterExpression = Expression.Parameter(typeof(Person), "observation");

			foreach (var column in columns.Where(c => !string.IsNullOrEmpty(c.SortDirection)))
			{
				// Get the property info for the sorting column
				var propertyInfo = typeof(Person).GetProperty(column.PropertyName);

				// Build the property expression
				var propertyExpression = Expression.Property(parameterExpression, propertyInfo);
				var sortExpression = Expression.Lambda<Func<Person, object>>(
					Expression.Convert(propertyExpression, typeof(object)), parameterExpression
				);

				// Apply the sorting
				if (orderedQuery == null)
				{
					// Initial sorting, use OrderBy or OrderByDescending
					orderedQuery = column.SortDirection.ToLower() == "asc"
						? query.OrderBy(sortExpression)
						: query.OrderByDescending(sortExpression);
				}
				else
				{
					// Subsequent sorting, use ThenBy or ThenByDescending
					orderedQuery = column.SortDirection.ToLower() == "asc"
						? orderedQuery.ThenBy(sortExpression)
						: orderedQuery.ThenByDescending(sortExpression);
				}
			}

			return orderedQuery ?? query.OrderBy(o => o.Id); // Default sorting if no valid columns
		}
	}
}
