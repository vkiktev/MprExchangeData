//-----------------------------------------------------------------------
// <copyright file="BaseExchangeTask.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using Ipk.Custom.MPR.Model;
using Ipk.Custom.MPR.Model.Models;
using log4net;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// Base abstract class for every exchange task 
    /// </summary>
    public abstract class BaseExchangeTask
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(BaseExchangeTask));

        public event EventHandler<ExchangeEventArgs> ExchnageEventCaused;

        /// <summary>
        /// The global Log object
        /// </summary>
        public ILog Log
        {
            get { return _log; }
        }

        /// <summary>
        /// Exchange entity
        /// </summary>
        public abstract ExchangeEntity ExchangeEntity { get; }

        /// <summary>
        /// Raise exchange event 
        /// </summary>
        /// <param name="status">Type of exchange status</param>
        /// <param name="comment">String comment</param>
        /// <param name="errorText">Error message</param>
        /// <param name="isFinish">Flag that is finish step</param>
        public void PublishEventLog(ExchangeStatusType status, string comment, string errorText, bool isFinish = false)
        {
            if (this.ExchnageEventCaused != null)
                this.ExchnageEventCaused(this, new ExchangeEventArgs(ExchangeEntity, status, comment, errorText, isFinish));
        }
    }
}
