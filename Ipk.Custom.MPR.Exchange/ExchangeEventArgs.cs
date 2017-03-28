//-----------------------------------------------------------------------
// <copyright file="ExchangeEventArgs.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using Ipk.Custom.MPR.Model;
using Ipk.Custom.MPR.Model.Models;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// EventArgs for Exchange events 
    /// </summary>
    public class ExchangeEventArgs : EventArgs
    {
        private ExchangeEntity _entity;
        private ExchangeStatusType _exchangeStatusType;
        private string _eventName;
        private string _comment;
        private string _errorText;
        private DateTime _dateTime;
        private Guid _entityUid;
        private ExchangeHistory _history;

        private bool _isFinish;

        /// <summary>
        /// Initialize a new object
        /// </summary>
        /// <param name="history">Exchange history item</param>
        /// <param name="isFinish">Flag that it is finish event</param>
        public ExchangeEventArgs(ExchangeHistory history, bool isFinish)
        {
            _history = history;
            _isFinish = isFinish;
        }

        /// <summary>
        /// Initialize a new object
        /// </summary>
        /// <param name="entity">Exchange entity</param>
        /// <param name="entityUid">Entity id</param>
        /// <param name="exchangeStatusType">Type of exchange status</param>
        /// <param name="comment">Comment if exists</param>
        /// <param name="errorText">Error text if exists</param>
        /// <param name="isFinish">Flag that it is finish event</param>
        public ExchangeEventArgs(ExchangeEntity entity, Guid entityUid, ExchangeStatusType exchangeStatusType, string comment, string errorText, bool isFinish):
            this(entity, exchangeStatusType, comment, errorText, isFinish)
        {
            _entityUid = entityUid;
        }

        /// <summary>
        /// Initialize a new object
        /// </summary>
        /// <param name="entity">Exchange entity</param>
        /// <param name="exchangeStatusType">Type of exchange status</param>
        /// <param name="comment">Comment if exists</param>
        /// <param name="errorText">Error text if exists</param>
        /// <param name="isFinish">Flag that it is finish event</param>
        public ExchangeEventArgs(ExchangeEntity entity, ExchangeStatusType exchangeStatusType, string comment, string errorText, bool isFinish)
        {
            _entity = entity;
            _exchangeStatusType = exchangeStatusType;
            _comment = comment;
            _errorText = errorText;
            _dateTime = DateTime.Now;
            _isFinish = isFinish;
        }

        /// <summary>
        /// Flag that it is finish event
        /// </summary>
        public bool IsFinish
        {
            get
            {
                return _isFinish;
            }
        }

        /// <summary>
        /// Exhange history item
        /// </summary>
        public ExchangeHistory ExchangeHistory
        {
            get
            {
                return _history != null ? _history : new ExchangeHistory() { ExchangeEntityUID = _entity.UID, EntityUID = _entityUid, ExchangeEntity = _entity, Comment = _comment, DateRecord = _dateTime, ErrorText = _errorText, ExchangeStatusType = _exchangeStatusType };
            }
        }

        /// <summary>
        /// ToString() implementation
        /// </summary>
        /// <returns>String object representation</returns>
        public override string ToString()
        {
            if(_history == null)
                return string.Format("ExchangeHistory is null, IsFinish is {0}", _isFinish);
            return string.Format("ExchangeHistory: DateRecord {0}, EntityName {1}, ExchangeStatusType {2}, Comment {3}, ErrorText {4}", _history.DateRecord, _history.EntityName, _history.ExchangeStatusType, _history.Comment, _history.ErrorText);
        }
    }
}
