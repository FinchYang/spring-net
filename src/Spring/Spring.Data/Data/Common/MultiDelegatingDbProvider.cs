#region Licence

/*
 * Copyright � 2002-2007 the original author or authors.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

using System;
using System.Collections;
using System.Data;
using Spring.Collections;
using Spring.Dao;
using Spring.Data.Common;
using Spring.Objects.Factory;
using Spring.Threading;


namespace Spring.Data
{
    /// <summary>
    /// A wrapper implementation for IDbProvider such that multiple DbProvider instances can be
    /// selected at runtime, say based on web request criteria.
    /// </summary>
    /// <remarks>
    /// The name of which DbProvider to use, as provided to the IDictionary property TargetDbProviders
    /// is "dbProviderName".  Once the target dbprovider name is known, set the name via a call to
    /// <code>LogicalThreadContext.SetData("dbProviderName", "database1ProviderName")</code>. The value
    /// "database1ProviderName" must match a key in the provided TargetDbProviders dictionary.
    /// </remarks>
    /// <author>Mark Pollack</author>
    public class MultiDelegatingDbProvider : IDbProvider, IInitializingObject
    {
        private IDictionary targetDbProviders = new SynchronizedHashtable();

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiDelegatingDbProvider"/> class.
        /// </summary>
        public MultiDelegatingDbProvider()
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiDelegatingDbProvider"/> class.
        /// </summary>
        /// <param name="targetDbProviders">The target db providers.</param>
        public MultiDelegatingDbProvider(IDictionary targetDbProviders)
        {
            this.targetDbProviders = targetDbProviders;
        }

        #endregion

        /// <summary>
        /// Sets the target db providers.
        /// </summary>
        /// <value>The target db providers.</value>
        public IDictionary TargetDbProviders
        {
            set { targetDbProviders = value; }
        }

        /// <summary>
        /// Ensures that the there are values in the TargetDbProviders dictionary and that the
        /// key is of the type string and value is of the type IDbProvider.
        /// </summary>
        /// <exception cref="ArgumentException">If the above conditions are not met.</exception>
        public void AfterPropertiesSet()
        {
            if (targetDbProviders.Count == 0)
            {
                throw new ArgumentException("Target DbProvider collection required.");
            }
            foreach (DictionaryEntry entry in targetDbProviders)
            {
                if (! entry.Key.GetType().Equals(typeof(string)))
                {
                    throw new ArgumentException("Key identifying target IDbProvider in TargetDbProviders dictionary property is required to be of type string.  Key = " + entry.Key);
                }
                IDbProvider targetProvider = entry.Value as IDbProvider;
                if (targetProvider == null)
                {
                    throw new ArgumentException("Value in TargetDbProviders dictionary is not of type IDbProvider.");
                }
            } 
        }
        
        #region IDbProvider methods


        /// <summary>
        /// Returns a new command object for executing SQL statments/Stored Procedures
        /// against the database.
        /// </summary>
        /// <returns>An new <see cref="IDbCommand"/></returns>
        public IDbCommand CreateCommand()
        {
            return GetTargetProvider().CreateCommand();
        }

        /// <summary>
        /// Returns a new connection object to communicate with the database.
        /// </summary>
        /// <returns>A new <see cref="IDbConnection"/></returns>
        public IDbConnection CreateConnection()
        {
            return GetTargetProvider().CreateConnection();
        }

        /// <summary>
        /// Returns a new parameter object for binding values to parameter
        /// placeholders in SQL statements or Stored Procedure variables.
        /// </summary>
        /// <returns>A new <see cref="IDbDataParameter"/></returns>
        public IDbDataParameter CreateParameter()
        {
            return GetTargetProvider().CreateParameter();
        }

        /// <summary>
        /// Returns a new adapter objects for use with offline DataSets.
        /// </summary>
        /// <returns>A new <see cref="IDbDataAdapter"/></returns>
        public IDbDataAdapter CreateDataAdapter()
        {
            return GetTargetProvider().CreateDataAdapter();
        }

        /// <summary>
        /// Returns a new instance of the providers CommandBuilder class.
        /// </summary>
        /// <returns>A new Command Builder</returns>
        /// <remarks>In .NET 1.1 there was no common base class or interface
        /// for command builders, hence the return signature is object to
        /// be portable (but more loosely typed) across .NET 1.1/2.0</remarks>
        public object CreateCommandBuilder()
        {
            return GetTargetProvider().CreateDataAdapter();
        }

        /// <summary>
        /// Creates the name of the parameter in the format appropriate to use inside IDbCommand.CommandText.
        /// </summary>
        /// <param name="name">The unformatted name of the parameter.</param>
        /// <returns>
        /// The parameter name formatted foran IDbCommand.CommandText.
        /// </returns>
        /// <remarks>In most cases this adds the parameter prefix to the name passed into this method.</remarks>
        public string CreateParameterName(string name)
        {
            return GetTargetProvider().CreateParameterName(name);
        }


        /// <summary>
        /// Creates the name ofthe parameter in the format appropriate for an IDataParameter, i.e. to be
        /// part of a IDataParameterCollection.
        /// </summary>
        /// <param name="name">The unformatted name of the parameter.</param>
        /// <returns>
        /// The parameter name formatted for an IDataParameter
        /// </returns>
        public string CreateParameterNameForCollection(string name)
        {
            return GetTargetProvider().CreateParameterNameForCollection(name);
        }

        /// <summary>
        /// Return metadata information about the database provider
        /// </summary>
        /// <value></value>
        public IDbMetadata DbMetadata
        {
            get { return GetTargetProvider().DbMetadata; }
        }

        /// <summary>
        /// Connection string used to create connections.
        /// </summary>
        /// <value></value>
        public string ConnectionString
        {
            get { return GetTargetProvider().ConnectionString; }
            set { GetTargetProvider().ConnectionString = value; }
        }

        /// <summary>
        /// Extracts the provider specific error code as a string.
        /// </summary>
        /// <param name="e">The data access exception.</param>
        /// <returns>The provider specific error code</returns>
        public string ExtractError(Exception e)
        {
            return GetTargetProvider().ExtractError(e);
        }

        /// <summary>
        /// Determines whether the provided exception is in fact related
        /// to database access.  This can be provider dependent in .NET 1.1 since
        /// there isn't a common base class for ADO.NET exceptions.
        /// </summary>
        /// <param name="e">The exception thrown when performing data access
        /// operations.</param>
        /// <returns>
        /// 	<c>true</c> if is a valid data access exception for the specified
        /// exception; otherwise, <c>false</c>.
        /// </returns>
        public bool IsDataAccessException(Exception e)
        {
#if NET_2_0
            if (e is System.Data.Common.DbException)
            {
                return true;
            }
            else
            {
                return false;
            }
#else
            return IsDataAccessExceptionBCL11(e);
#endif
        }

        /// <summary>
        /// Determines whether is data access exception in .NET 1.1 for the specified exception.
        /// </summary>
        /// <param name="e">The candidate exception.</param>
        /// <returns>
        /// 	<c>true</c> if is data access exception in .NET 1.1 for the specified exception; otherwise, <c>false</c>.
        /// </returns>
        public bool IsDataAccessExceptionBCL11(Exception e)
        {
            return false;
        }
        #endregion

        /// <summary>
        /// Gets the target provider based on the thread local name "dbProviderName"
        /// </summary>
        /// <returns>The corresonding IDbProvider.</returns>
        protected virtual IDbProvider GetTargetProvider()
        {
            string dbProviderName = (string)LogicalThreadContext.GetData("dbProviderName");
            if (targetDbProviders.Contains(dbProviderName))
            {
                return (IDbProvider)targetDbProviders[dbProviderName];
            }
            throw new InvalidDataAccessApiUsageException("'" + dbProviderName + "'"
                                        + "was not under the thread local key 'dbProviderName'");
        }
    }
}