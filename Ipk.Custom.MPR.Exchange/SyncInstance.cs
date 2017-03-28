//-----------------------------------------------------------------------
// <copyright file="SyncInstance.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Ipk.Custom.MPR.Data.Repository;
using Ipk.Custom.MPR.Model;
using Ipk.Custom.MPR.Model.Models;
using Ipk.Custom.MPR.Repository.Base;
using log4net;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// Class for managing synchronization process
    /// </summary>
    public class SyncInstance
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof (SyncInstance));
        private readonly string _argoConnectionString;
        private readonly string _mprConnectionString;
        private bool _stopped;
        private bool _stopping;

        /// <summary>
        /// Initialize new exchange instance 
        /// </summary>
        /// <param name="argoConnectionString">Source's database connection string</param>
        /// <param name="mprConnectionString">Destination's database connection strings</param>
        public SyncInstance(string argoConnectionString, string mprConnectionString)
        {
            _argoConnectionString = argoConnectionString;
            _mprConnectionString = mprConnectionString;
        }

        public event EventHandler<ExchangeEventArgs> ExchnageEventCaused;

        /// <summary>
        /// Sets stop flag for breaking exchange
        /// </summary>
        public void TryStop()
        {
            _stopping = true;
        }

        /// <summary>
        /// Method which start process synchronization
        /// </summary>
        public void StartExchange()
        {
            var unitOfWork = new UnitOfWork(_mprConnectionString);
            var repository = unitOfWork.ExchangeEntityRepository;

            var exchangeEnitites = repository.GetList().OrderBy(x => x.Priority).ToList();

            var tasks = PrepareTasks();

            var argoConnection = new SqlConnection(_argoConnectionString);

            foreach (var ee in exchangeEnitites)
            {
                var task = tasks.FirstOrDefault(x => x.EntityName == ee.EntityName);

                if (task != null)
                {
                    ((BaseExchangeTask) task).ExchnageEventCaused += Task_ExchnageEventCaused;
                    task.PrepareArgoData(argoConnection, ee);
                    task.ApplyChanges(unitOfWork);
                    UpdateExchangeEntity(repository, ee, task.GetMaxTimeStamp());
                    if (!_stopping) continue;
                    WriteInfoLog(string.Format("Exchange is interrupt. Last finished task is {0}", ee.EntityName));
                    ExchnageEventCaused(this,
                        new ExchangeEventArgs(
                            new ExchangeHistory
                            {
                                DateRecord = DateTime.Now,
                                Comment = "Обмен с базой данных АРГО прерван",
                                ExchangeStatusType = ExchangeStatusType.Unknown
                            }, true));
                    return;
                }
                WriteErrorLog(string.Format("Task with name {0} is didn't found in tasks list.", ee.EntityName),null);
            }
            if (ExchnageEventCaused != null)
                ExchnageEventCaused(this,
                    new ExchangeEventArgs(
                        new ExchangeHistory
                        {
                            DateRecord = DateTime.Now,
                            Comment = "Обмен с базой данных АРГО завершен",
                            ExchangeStatusType = ExchangeStatusType.Unknown
                        }, true));
        }

        /// <summary>
        /// Method for preparing list of tasks for sync
        /// </summary>
        /// <returns></returns>
        private IList<IExchangeTask> PrepareTasks()
        {
            IList<IExchangeTask> list = new List<IExchangeTask>();

            list.Add(new TransferSheetExchangeTask());
            list.Add(new CompanyExchangeTask());
            list.Add(new DivisionExchangeTask());
            list.Add(new JewelryMetalExchangeTask());
            list.Add(new JewelryProofExchangeTask());
            list.Add(new JewelryTypeExchangeTask());

            return list;
        }

        private void Task_ExchnageEventCaused(object sender, ExchangeEventArgs e)
        {
            if (ExchnageEventCaused != null)
                ExchnageEventCaused(this, new ExchangeEventArgs(e.ExchangeHistory, false));
        }

        /// <summary>
        /// Update last timestamp for entity
        /// </summary>
        /// <param name="repository"></param>
        /// <param name="entity"></param>
        /// <param name="lastState"></param>
        private void UpdateExchangeEntity(IExchangeEntityRepository repository, ExchangeEntity entity, byte[] lastState)
        {
            if (BitConverter.ToInt64(lastState, 0) == 0)
                return;
            entity.LastState = lastState;
            repository.Update(entity);
            repository.Save();
        }

        private void WriteErrorLog(string text, Exception ex)
        {
            if (ex == null)
                _log.Error(text);
            else
                _log.Error(text, ex);
        }

        private void WriteInfoLog(string text)
        {
            _log.Info(text);
        }
    }
}