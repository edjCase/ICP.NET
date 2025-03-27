using EdjCase.ICP.Candid.Exceptions;
using EdjCase.ICP.Candid.Mapping;
using EdjCase.ICP.Candid.Mapping.Mappers;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Models.Types;
using EdjCase.ICP.Candid.Models.Values;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace EdjCase.ICP.Candid
{
	/// <summary>
	/// A class that converts to and from C# and Candid types
	/// </summary>
	public class CandidConverter
	{
		/// <summary>
		/// A candid converter with the default settings
		/// </summary>
		public static CandidConverter Default { get; } = new CandidConverter();

		private readonly CandidConverterOptions _options;

		private readonly ConcurrentDictionary<Type, ICandidValueMapper> _typeToMapperCache;



		/// <param name="options">Optional. The options for the converter. If not set, will use defaults</param>
		public CandidConverter(CandidConverterOptions? options = null)
		{
			this._options = options ?? new CandidConverterOptions();
			this._typeToMapperCache = new ConcurrentDictionary<Type, ICandidValueMapper>();
		}
		/// <param name="configureOptions">Configure function for the converter options. Creates default options</param>

		public CandidConverter(Action<CandidConverterOptions> configureOptions) : this()
		{
			configureOptions(this._options);
		}

		/// <summary>
		/// Converts a C# object into a candid value
		/// </summary>
		/// <param name="obj">The object to convert</param>
		/// <returns>Candid value mapped from the object</returns>
		public CandidValue FromObject(object obj)
		{
			if (ReferenceEquals(obj, null))
			{
				throw new ArgumentNullException(nameof(obj));
			}
			ICandidValueMapper mapper = this.ResolveMapper(obj.GetType());

			try
			{
				return mapper!.Map(obj, this);
			}
			catch (Exception e)
			{
				throw new Exception($"Failed to convert object '{obj.GetType().FullName}' to candid value", e);
			}
		}

		/// <summary>
		/// Converts a C# object into a typed candid value
		/// </summary>
		/// <typeparam name="T">Type of the object</typeparam>
		/// <param name="obj">The object to convert</param>
		/// <returns>Candid typed value mapped from the object</returns>
		public CandidTypedValue FromTypedObject<T>(T obj)
			where T : notnull
		{
			CandidValue value = this.FromObjectInternal<T>(obj, out ICandidValueMapper mapper);
			CandidType type = mapper.GetMappedCandidType(typeof(T)) ?? throw new InvalidOperationException($"Type '{typeof(T).FullName}' does not map to a candid type");
			return new CandidTypedValue(value, type);
		}


		private CandidValue FromObjectInternal<T>(object obj, out ICandidValueMapper mapper)
		{
			mapper = this.ResolveMapper(typeof(T));

			try
			{
				return mapper.Map(obj, this);
			}
			catch (Exception e)
			{
				throw new Exception($"Failed to convert object '{typeof(T).FullName}' to candid value", e);
			}
		}

		/// <summary>
		/// Converts a candid value into a C# object
		/// </summary>
		/// <typeparam name="T">The C# type to convert to</typeparam>
		/// <param name="value">The candid value to convert</param>
		/// <returns>A C# object of the value</returns>
		public T ToObject<T>(CandidValue value)
		{
			return (T)this.ToObject(typeof(T), value);
		}

		/// <summary>
		/// Converts a candid opt value to an OptionalValue
		/// </summary>
		/// <typeparam name="T">The C# type to convert the inner type to</typeparam>
		/// <param name="value">The opt value</param>
		/// <returns>An optional value</returns>
		public OptionalValue<T> ToOptionalObject<T>(CandidOptional value)
		{
			return this.ToOptionalObject(typeof(T), value).Cast<T>();
		}


		/// <summary>
		/// Converts a candid value into a C# object
		/// </summary>
		/// <param name="objType">The C# type to convert to</param>
		/// <param name="value">The candid value to convert</param>
		/// <returns>A C# object of the value</returns>
		public object ToObject(Type objType, CandidValue value)
		{
			ICandidValueMapper mapper = this.ResolveMapper(objType);
			try
			{
				return mapper!.Map(value, this);
			}
			catch (Exception e)
			{
				throw new Exception($"Failed to convert candid value '{value}' to object of type {objType.FullName}", e);
			}
		}

		/// <summary>
		/// Converts a candid opt value to an OptionalValue
		/// </summary>
		/// <param name="objType">The C# type to convert the inner type to</param>
		/// <param name="value">The opt value</param>
		/// <returns>An optional value</returns>
		public OptionalValue<object> ToOptionalObject(Type objType, CandidOptional value)
		{
			if (value.IsNull())
			{
				return OptionalValue<object>.NoValue(); // Handle null here for all mappers
			}
			object v = this.ToObject(objType, value.Value);
			return OptionalValue<object>.WithValue(v);
		}



		private ICandidValueMapper ResolveMapper(Type type)
		{
			return this._typeToMapperCache.GetOrAdd(type, this.ResolveUncached);
		}

		private ICandidValueMapper ResolveUncached(Type type)
		{
			ICandidValueMapper? mapper = null;
			if (this._options.CustomMappers != null)
			{
				foreach (ICandidValueMapper customMapper in this._options.CustomMappers)
				{
					CandidType? mappedType = customMapper.GetMappedCandidType(type);
					if (mappedType != null)
					{
						mapper = customMapper;
						break;
					}
				}
			}

			return mapper == null
				// Create a dynamic mapper if no custom one
				? DefaultMapperFactory.Build(type)
				: mapper;
		}

	}


}
