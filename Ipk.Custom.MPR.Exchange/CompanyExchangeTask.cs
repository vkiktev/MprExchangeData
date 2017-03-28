//-----------------------------------------------------------------------
// <copyright file="CompanyExchangeTask.cs" author="Slava Kiktev">
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
    /// Descripts rules for updating Company table 
    /// </summary>
    public class CompanyExchangeTask : BaseExchangeTask, IExchangeTask
    {
        private DataTable _dataTable;
        private SqlConnection _argoConnection;
        private ExchangeEntity _exchangeEntity;

        private byte[] _lastStamp;

        /// <summary>
        /// Ctor
        /// </summary>
        public CompanyExchangeTask()
        {
            _lastStamp = new byte[8];
            _dataTable = new DataTable();
        }

        /// <summary>
        /// Entity name
        /// </summary>
        public string EntityName
        {
            get { return "Company"; }
        }


        /// <summary>
        /// Exchange entity
        /// </summary>
        public override ExchangeEntity ExchangeEntity
        {
            get { return _exchangeEntity; }
        }

        /// <summary>
        /// Method for loading data from source
        /// </summary>
        /// <param name="argoConnection">Source side connection string</param>
        /// <param name="exchangeEntity">Object of exchange with historic info (last timestamp)</param>
        public void PrepareArgoData(System.Data.SqlClient.SqlConnection argoConnection, Model.Models.ExchangeEntity exchangeEntity)
        {
            try
            {
                _argoConnection = argoConnection;
                _exchangeEntity = exchangeEntity;

                _lastStamp = exchangeEntity.LastState;

                PublishEventLog(ExchangeStatusType.Unknown, "Запрос данных \"Юридические лица\" из АРГО", null);

                if (_argoConnection.State != ConnectionState.Open)
                    _argoConnection.Open();

                string cmdText = @"SELECT  [UID]
                                      ,[Code]
                                      ,[Ogrn]
                                      ,[Inn]
                                      ,[Kpp]
                                      ,[Name]
                                      ,[FirmName]
                                      ,'' as [Okpo]
                                      ,'' as [Okud]
                                      ,[Address]
                                      ,[IsDeleted]
                                      ,[State]
                               FROM [dbo].[Company]
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
                Log.Error("CompanyExchangeTask. PrepareArgoData Method Error. ", ex);
                throw;
            }
        }

        /// <summary>
        /// Method for creating new company from DataRow 
        /// </summary>
        /// <param name="row">Row contains data</param>
        /// <returns>The new Company object</returns>
        private Company NewCompany(DataRow row)
        {
            Company company = new Company();
            company.UID = Guid.Parse(row["UID"].ToString());
            company.IsDeleted = Convert.ToBoolean(row["IsDeleted"]);
            company.Name = row["Name"].ToString();
            if (!row.IsNull("FirmName"))
                company.FullName = row["FirmName"].ToString();
            if (!row.IsNull("Code"))
                company.Code = row["Code"].ToString();
            if (!row.IsNull("Inn"))
                company.Inn = row["Inn"].ToString();
            if (!row.IsNull("Ogrn"))
                company.Ogrn = row["Ogrn"].ToString();
            if (!row.IsNull("Kpp"))
                company.Kpp = row["Kpp"].ToString();
            if (!row.IsNull("Okpo"))
                company.Okpo = row["Okpo"].ToString();
            if (!row.IsNull("Okud"))
                company.Okud = row["Okud"].ToString();
            if (!row.IsNull("Address"))
                company.Address = row["Address"].ToString();

            return company;
        }

        /// <summary>
        /// Method for saving data to destination
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        public void ApplyChanges(UnitOfWork unitOfWork)
        {
            try
            {
                var repository = unitOfWork.CompanyRepository;
                var companies = repository.GetFullList();
                foreach (DataRow row in _dataTable.Rows)
                {
                    byte[] lastStamp = (byte[]) row["State"];
                    if (new SqlBinary(lastStamp) > new SqlBinary(_lastStamp))
                        _lastStamp = lastStamp;

                    var company = NewCompany(row);

                    var existCompany = companies.FirstOrDefault(x => x.UID == company.UID);

                    if (existCompany != null)
                    {
                        using (var t = new TransactionScope())
                        {
                            existCompany.IsDeleted = company.IsDeleted;
                            existCompany.Code = company.Code;
                            existCompany.FullName = company.FullName;
                            existCompany.Inn = company.Inn;
                            existCompany.IsBuyer = company.IsBuyer;
                            existCompany.Kpp = company.Kpp;
                            existCompany.Name = company.Name;
                            existCompany.Ogrn = company.Ogrn;

                            if (!string.IsNullOrWhiteSpace(company.Okpo))
                                existCompany.Okpo = company.Okpo;
                            if (!string.IsNullOrWhiteSpace(company.Okud))
                                existCompany.Okud = company.Okud;

                            repository.Update(existCompany);
                            repository.Save();

                            t.Complete();
                        }

                        PublishEventLog(ExchangeStatusType.Update, "Успешное обновление", null);
                    }
                    else
                    {
                        repository.Add(company);
                        repository.Save();
                        PublishEventLog(ExchangeStatusType.Insert, "Успешная вставка", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("TransferSheetExchangeTask. ApplyChanges Method Error. ", ex);
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
