//-----------------------------------------------------------------------
// <copyright file="DivisionExchangeTask.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Transactions;
using Ipk.Custom.MPR.Model;
using Ipk.Custom.MPR.Model.Models;
using Ipk.Custom.MPR.Repository.Base;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// Descripts rules for updating Division table 
    /// </summary>
    public class DivisionExchangeTask : BaseExchangeTask, IExchangeTask
    {
        private DataTable _dataTable;
        private SqlConnection _argoConnection;
        private ExchangeEntity _exchangeEntity;

        private byte[] _lastStamp;

        /// <summary>
        /// Ctor
        /// </summary>
        public DivisionExchangeTask()
        {
            _lastStamp = new byte[8];
            _dataTable = new DataTable();
        }

        /// <summary>
        /// Entity name
        /// </summary>
        public string EntityName
        {
            get { return "Division"; }
        }

        /// <summary>
        /// Exchange entity
        /// </summary>
        public override ExchangeEntity ExchangeEntity
        {
            get
            {
                return _exchangeEntity;
            }
        }


        /// <summary>
        /// Method for loading data from source
        /// </summary>
        /// <param name="argoConnection">Source side connection string</param>
        /// <param name="exchangeEntity">Object of exchange with historic info (last timestamp)</param>
        public void PrepareArgoData(System.Data.SqlClient.SqlConnection argoConnection, Model.Models.ExchangeEntity exchangeEntity)
        {
            _argoConnection = argoConnection;
            _exchangeEntity = exchangeEntity;

            _lastStamp = exchangeEntity.LastState;

            PublishEventLog(ExchangeStatusType.Unknown, "Запрос данных \"Отделения\" из АРГО", null);

            try
            {
                if (_argoConnection.State != ConnectionState.Open)
                    _argoConnection.Open();

                string cmdText = @"SELECT  [UID]
                                      ,[Code]
                                      ,[Name]
                                      ,[ShortName]
                                      ,[CompanyUID]
                                      ,[Code1C]
                                      ,[IsDeleted]
                                      ,[State]
                               FROM [dbo].[Division]
                               WHERE [State] > @State";

                SqlCommand command = new SqlCommand(cmdText, argoConnection);
                command.Parameters.AddWithValue("@State", _exchangeEntity.LastState);

                SqlDataAdapter adapter = new SqlDataAdapter(command);

                _dataTable.Clear();

                adapter.Fill(_dataTable);

                PublishEventLog(ExchangeStatusType.Unknown, string.Format("Подготовлено {0} записей для обновления", _dataTable.Rows.Count), null);
            }
            catch (Exception ex)
            {
                Log.Error("DivisionExchangeTask Error. ", ex);
                throw;
            }
        }

        /// <summary>
        /// Method for saving data to destination
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        public void ApplyChanges(UnitOfWork unitOfWork)
        {
            try
            {
                var repository = unitOfWork.DivisionRepository;
                var divisions = repository.GetList();
                foreach (DataRow row in _dataTable.Rows)
                {
                    byte[] lastStamp = (byte[]) row["State"];

                    if (new SqlBinary(lastStamp) > new SqlBinary(_lastStamp))
                        _lastStamp = lastStamp;

                    var division = NewDivision(row);

                    PublishEventLog(ExchangeStatusType.Unknown, string.Format("New division: {0}, {1}", division.UID, division.Code), null);

                    var existDivision = divisions.FirstOrDefault(x => x.UID == division.UID);

                    PublishEventLog(ExchangeStatusType.Unknown,
                                    string.Format("Exists division: {0}, {1}, {2}",
                                                  existDivision != null ? division.UID : Guid.Empty,
                                                  existDivision != null ? division.Code : "null"), null);

                    if (existDivision != null)
                    {
                        using (var t = new TransactionScope())
                        {
                            existDivision.IsDeleted = division.IsDeleted;
                            existDivision.Code = division.Code;
                            existDivision.Code1C = division.Code1C;
                            existDivision.CompanyUID = division.CompanyUID;
                            existDivision.Name = division.Name;
                            existDivision.ShortName = division.ShortName;

                            repository.Update(existDivision);
                            repository.Save();

                            t.Complete();
                        }
                        PublishEventLog(ExchangeStatusType.Update, "Успешное обновление", null);
                    }
                    else
                    {
                        repository.Add(division);
                        repository.Save();
                        PublishEventLog(ExchangeStatusType.Insert, "Успешная вставка", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("ApplyChanges Error. ", ex);
                throw;
            }
        }

        /// <summary>
        /// Method for creating new Division from DataRow 
        /// </summary>
        /// <param name="row">Row contains data</param>
        /// <returns>The new Division object</returns>
        private Division NewDivision(DataRow row)
        {
            Division division = new Division();
            division.UID = Guid.Parse(row["UID"].ToString());
            division.IsDeleted = Convert.ToBoolean(row["IsDeleted"]);
            division.Name = row["Name"].ToString();
            if (!row.IsNull("Code"))
                division.Code = row["Code"].ToString();
            if (!row.IsNull("Code1C"))
                division.Code1C = row["Code1C"].ToString();

            if (!row.IsNull("CompanyUID"))
                division.CompanyUID = Guid.Parse(row["CompanyUID"].ToString());

            if (!row.IsNull("ShortName"))
                division.ShortName = row["ShortName"].ToString();

            return division;
        }

        /// <summary>
        /// Gets maximum timestamp for preparing query
        /// </summary>
        /// <returns>Timestamp</returns>
        public byte[] GetMaxTimeStamp()
        {
            return _lastStamp;
        }
    }
}
