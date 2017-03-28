//-----------------------------------------------------------------------
// <copyright file="JewelryProofExchangeTask.cs" author="Slava Kiktev">
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
    /// Descripts rules for updating JewelryProof table 
    /// </summary>
    public class JewelryProofExchangeTask : BaseExchangeTask, IExchangeTask
    {
        private DataTable _dataTable;
        private SqlConnection _argoConnection;
        private ExchangeEntity _exchangeEntity;

        private byte[] _lastStamp;

        /// <summary>
        /// Ctor
        /// </summary>
        public JewelryProofExchangeTask()
        {
            _lastStamp = new byte[8];
            _dataTable = new DataTable();
        }

        /// <summary>
        /// Entity name
        /// </summary>
        public string EntityName
        {
            get { return "JewelryProof"; }
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
        /// <param name="argoConnection">Source's connetion string</param>
        /// <param name="exchangeEntity">Object of exchange with historic info (last timestamp)</param>
        public void PrepareArgoData(System.Data.SqlClient.SqlConnection argoConnection, Model.Models.ExchangeEntity exchangeEntity)
        {
            try
            {
                _argoConnection = argoConnection;
                _exchangeEntity = exchangeEntity;

                _lastStamp = exchangeEntity.LastState;

                PublishEventLog(ExchangeStatusType.Unknown, "Запрос данных \"Пробы\" из АРГО", null);

                if (_argoConnection.State != ConnectionState.Open)
                    _argoConnection.Open();

                string cmdText = @"SELECT  [UID]
                                      ,[Code]
                                      ,[Name]
                                      ,[JewelryMetalUID]
                                      ,[Cleanness]
                                      ,[IsDeleted]
                                      ,[State]
                               FROM [dbo].[JewelryProof]
                               WHERE [State] > @State";

                SqlCommand command = new SqlCommand(cmdText, argoConnection);
                command.Parameters.AddWithValue("@State", _exchangeEntity.LastState);

                SqlDataAdapter adapter = new SqlDataAdapter(command);

                _dataTable.Clear();

                adapter.Fill(_dataTable);

                PublishEventLog(ExchangeStatusType.Unknown,
                                string.Format("Подготовлено {0} записей для обновления", _dataTable.Rows.Count), null);
            }
            catch (Exception ex)
            {
                Log.Error("JewelryProofExchangeTask. PrepareArgoData Method Error. ", ex);
                throw;
            }
        }

        /// <summary>
        /// Method for creating new JewelryProof from DataRow 
        /// </summary>
        /// <param name="row">Row contains data</param>
        /// <returns>The new JewelryProof object</returns>
        private JewelryProof NewJewelryProof(DataRow row)
        {
            JewelryProof proof = new JewelryProof();
            proof.UID = Guid.Parse(row["UID"].ToString());
            proof.IsDeleted = Convert.ToBoolean(row["IsDeleted"]);
            proof.Name = row["Name"].ToString();
            if (!row.IsNull("Cleanness"))
                proof.Cleanness = Convert.ToDecimal(row["Cleanness"]);
            if (!row.IsNull("Code"))
                proof.Code = row["Code"].ToString();
            if (!row.IsNull("JewelryMetalUID"))
                proof.JewelryMetalUID = Guid.Parse(row["JewelryMetalUID"].ToString());

            return proof;
        }

        /// <summary>
        /// Method for saving data to destination
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        public void ApplyChanges(UnitOfWork unitOfWork)
        {
            try
            {
                var repository = unitOfWork.JewelryProofRepository;
                var jewelryproofs = repository.GetFullList();
                foreach (DataRow row in _dataTable.Rows)
                {
                    byte[] lastStamp = (byte[]) row["State"];
                    if (new SqlBinary(lastStamp) > new SqlBinary(_lastStamp))
                        _lastStamp = lastStamp;

                    var proof = NewJewelryProof(row);

                    var existProof = jewelryproofs.FirstOrDefault(x => x.UID == proof.UID);

                    if (existProof != null)
                    {
                        using (var t = new TransactionScope())
                        {
                            existProof.IsDeleted = proof.IsDeleted;
                            existProof.Name = proof.Name;
                            existProof.Cleanness = proof.Cleanness;
                            existProof.Code = proof.Code;
                            existProof.JewelryMetalUID = proof.JewelryMetalUID;

                            repository.Update(existProof);
                            repository.Save();

                            t.Complete();
                        }
                        PublishEventLog(ExchangeStatusType.Update, "Успешное обновление", null);
                    }
                    else
                    {
                        repository.Add(proof);
                        repository.Save();
                        PublishEventLog(ExchangeStatusType.Insert, "Успешная вставка", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("JewelryProofExchangeTask. ApplyChanges Method Error. ", ex);
                throw;
            }
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
