//-----------------------------------------------------------------------
// <copyright file="IExchangeTask.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using Ipk.Custom.MPR.Model.Models;
using Ipk.Custom.MPR.Repository.Base;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// Interface for exchanging task
    /// </summary>
    public interface IExchangeTask
    {
        /// <summary>
        /// Entity Name
        /// </summary>
        string EntityName {get;}

        /// <summary>
        /// Method for loading data from source
        /// </summary>
        /// <param name="argoConnection">Source's connetion string</param>
        /// <param name="exchangeEntity">Object of exchange with historic info (last timestamp)</param>
        void PrepareArgoData(System.Data.SqlClient.SqlConnection argoConnection, ExchangeEntity exchangeEntity);

        /// <summary>
        /// Method for saving data to destination
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        void ApplyChanges(UnitOfWork unitOfWork);

        /// <summary>
        /// Method for getting last timestamp for entity
        /// </summary>
        /// <returns>Timestamp</returns>
        byte[] GetMaxTimeStamp(); 
    }
}
