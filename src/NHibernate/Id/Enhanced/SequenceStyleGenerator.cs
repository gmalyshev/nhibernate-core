using System.Collections.Generic;

using NHibernate.Engine;
using NHibernate.Mapping;
using NHibernate.Type;
using NHibernate.Util;

namespace NHibernate.Id.Enhanced
{
	/// <summary>
	/// Generates identifier values based on an sequence-style database structure.
	/// Variations range from actually using a sequence to using a table to mimic
	/// a sequence. These variations are encapsulated by the <see cref="IDatabaseStructure"/>
	/// interface internally.
	/// </summary>
	/// <remarks>
	/// General configuration parameters:
	/// <table>
	///   <tr>
	///     <td><b>NAME</b></td>
	///     <td><b>DEFAULT</b></td>
	///     <td><b>DESCRIPTION</b></td>
	///   </tr>
	///   <tr>
	///     <td><see cref="SequenceParam"/></td>
	///     <td><see cref="DefaultSequenceName"/></td>
	///     <td>The name of the sequence/table to use to store/retrieve values</td>
	///   </tr>
	///   <tr>
	///     <td><see cref="InitialParam"/></td>
	///     <td><see cref="DefaultInitialValue"/></td>
	///     <td>The initial value to be stored for the given segment; the effect in terms of storage varies based on <see cref="Optimizer"/> and <see cref="DatabaseStructure"/></td>
	///   </tr>
	///   <tr>
	///     <td><see cref="IncrementParam"/></td>
	///     <td><see cref="DefaultIncrementSize"/></td>
	///     <td>The increment size for the underlying segment; the effect in terms of storage varies based on <see cref="Optimizer"/> and <see cref="DatabaseStructure"/></td>
	///   </tr>
	///   <tr>
	///     <td><see cref="OptimizerParam"/></td>
	///     <td><i>depends on defined increment size</i></td>
	///     <td>Allows explicit definition of which optimization strategy to use</td>
	///   </tr>
	///     <td><see cref="ForceTableParam"/></td>
	///     <td><b><i>false<i/></b></td>
	///     <td>Allows explicit definition of which optimization strategy to use</td>
	///   </tr>
	/// </table>
	/// <p/>
	/// Configuration parameters used specifically when the underlying structure is a table:
	/// <table>
	///   <tr>
	///     <td><b>NAME</b></td>
	///     <td><b>DEFAULT</b></td>
	///     <td><b>DESCRIPTION</b></td>
	///   </tr>
	///   <tr>
	///     <td><see cref="ValueColumnParam"/></td>
	///     <td><see cref="DefaultValueColumnName"/></td>
	///     <td>The name of column which holds the sequence value for the given segment</td>
	///   </tr>
	/// </table>
	/// </remarks>
	public class SequenceStyleGenerator : IPersistentIdentifierGenerator, IConfigurable
	{
		private static readonly IInternalLogger Log = LoggerProvider.LoggerFor(typeof(SequenceStyleGenerator));

		public const string DefaultSequenceName = "hibernate_sequence";
		public const int DefaultInitialValue = 1;
		public const int DefaultIncrementSize = 1;

		public const string SequenceParam = "sequence_name";
		public const string InitialParam = "initial_value";
		public const string IncrementParam = "increment_size";
		public const string OptimizerParam = "optimizer";
		public const string ForceTableParam = "force_table_use";
		public const string ValueColumnParam = "value_column";
		public const string DefaultValueColumnName = "next_val";

		public IDatabaseStructure DatabaseStructure { get; private set; }
		public IOptimizer Optimizer { get; private set; }
		public IType IdentifierType { get; private set; }


		#region Implementation of IConfigurable

		public virtual void Configure(IType type, IDictionary<string, string> parms, Dialect.Dialect dialect)
		{
			IdentifierType = type;

			bool forceTableUse = PropertiesHelper.GetBoolean(ForceTableParam, parms, false);

			string sequenceName = DetermineSequenceName(parms, dialect);

			int initialValue = DetermineInitialValue(parms);
			int incrementSize = DetermineIncrementSize(parms);
			string valueColumnName = DetermineValueColumnName(parms, dialect);

			string optimizationStrategy = DetermineOptimizationStrategy(parms, incrementSize);
			incrementSize = DetermineAdjustedIncrementSize(optimizationStrategy, incrementSize);

			if (dialect.SupportsSequences && !forceTableUse)
			{
				if (OptimizerFactory.Pool.Equals(optimizationStrategy) && !dialect.SupportsPooledSequences)
				{
					// TODO : may even be better to fall back to a pooled table strategy here so that the db stored values remain consistent...
					optimizationStrategy = OptimizerFactory.HiLo;
				}
				DatabaseStructure = new SequenceStructure(dialect, sequenceName, initialValue, incrementSize);
			}
			else
			{
				DatabaseStructure = new TableStructure(dialect, sequenceName, valueColumnName, initialValue, incrementSize);
			}

			Optimizer = OptimizerFactory.BuildOptimizer(
				optimizationStrategy,
				IdentifierType.ReturnedClass,
				incrementSize,
				PropertiesHelper.GetInt32(InitialParam, parms, -1)); // Use -1 as default initial value here to signal that it's not set.

			DatabaseStructure.Prepare(Optimizer);
		}


		/// <summary>
		/// Determine the name of the sequence (or table if this resolves to a physical table) to use.
		/// Called during configuration.
		/// </summary>
		/// <param name="parms"></param>
		/// <param name="dialect"></param>
		/// <returns></returns>
		protected string DetermineSequenceName(IDictionary<string, string> parms, Dialect.Dialect dialect)
		{
			string sequenceName = PropertiesHelper.GetString(SequenceParam, parms, DefaultSequenceName);
			if (sequenceName.IndexOf('.') < 0)
			{
				string schemaName;
				string catalogName;
				parms.TryGetValue(PersistentIdGeneratorParmsNames.Schema, out schemaName);
				parms.TryGetValue(PersistentIdGeneratorParmsNames.Catalog, out catalogName);
				sequenceName = Table.Qualify(catalogName, schemaName, sequenceName);
			}
			else
			{
				// If already qualified there is not much we can do in a portable manner so we pass it
				// through and assume the user has set up the name correctly.
			}

			return sequenceName;
		}


		/// <summary>
		/// Determine the name of the column used to store the generator value in
		/// the db. Called during configuration, if a physical table is being used.
		/// </summary>
		protected string DetermineValueColumnName(IDictionary<string, string> parms, Dialect.Dialect dialect)
		{
			return PropertiesHelper.GetString(ValueColumnParam, parms, DefaultValueColumnName);
		}


		/// <summary>
		/// Determine the initial sequence value to use. This value is used when
		/// initializing the database structure (i.e. sequence/table). Called
		/// during configuration.
		/// </summary>
		protected int DetermineInitialValue(IDictionary<string, string> parms)
		{
			return PropertiesHelper.GetInt32(InitialParam, parms, DefaultInitialValue);
		}


		/// <summary>
		/// Determine the increment size to be applied. The exact implications of
		/// this value depends on the optimizer being used. Called during configuration.
		/// </summary>
		protected int DetermineIncrementSize(IDictionary<string, string> parms)
		{
			return PropertiesHelper.GetInt32(IncrementParam, parms, DefaultIncrementSize);
		}


		/// <summary>
		/// Determine the optimizer to use. Called during configuration.
		/// </summary>
		protected string DetermineOptimizationStrategy(IDictionary<string, string> parms, int incrementSize)
		{
			string defOptStrategy = incrementSize <= 1 ? OptimizerFactory.None : OptimizerFactory.Pool;
			return PropertiesHelper.GetString(OptimizerParam, parms, defOptStrategy);
		}


		/// <summary>
		/// In certain cases we need to adjust the increment size based on the
		/// selected optimizer. This is the hook to achieve that.
		/// </summary>
		/// <param name="optimizationStrategy">The determined optimizer strategy.</param>
		/// <param name="incrementSize">The determined, unadjusted, increment size.</param>
		protected int DetermineAdjustedIncrementSize(string optimizationStrategy, int incrementSize)
		{
			if (OptimizerFactory.None.Equals(optimizationStrategy) && incrementSize > 1)
			{
				Log.Warn("config specified explicit optimizer of [" + OptimizerFactory.None + "], but [" + IncrementParam + "=" + incrementSize + "; honoring optimizer setting");
				incrementSize = 1;
			}
			return incrementSize;
		}

		#endregion


		#region Implementation of IIdentifierGenerator

		public virtual object Generate(ISessionImplementor session, object obj)
		{
			return Optimizer.Generate(DatabaseStructure.BuildCallback(session));
		}

		#endregion


		#region Implementation of IPersistentIdentifierGenerator

		public virtual string GeneratorKey()
		{
			return DatabaseStructure.Name;
		}

		public virtual string[] SqlCreateStrings(Dialect.Dialect dialect)
		{
			return DatabaseStructure.SqlCreateStrings(dialect);
		}

		public virtual string[] SqlDropString(Dialect.Dialect dialect)
		{
			return DatabaseStructure.SqlDropStrings(dialect);
		}

		#endregion
	}
}