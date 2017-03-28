//-----------------------------------------------------------------------
// <copyright file="TransferSheetExchangeTask.cs" author="Slava Kiktev">
//
// Copyright © 2016 Slava Kiktev.  All rights reserved.
//
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Linq;
using System.Transactions;
using Ipk.Custom.MPR.Model;
using Ipk.Custom.MPR.Model.Models;
using Ipk.Custom.MPR.Repository.Base;

namespace Ipk.Custom.MPR.Exchange
{
    /// <summary>
    /// Descript rules for updating TransferSheet table
    /// </summary>
    public class TransferSheetExchangeTask : BaseExchangeTask, IExchangeTask
    {
        private DataTable _dataTable;
        private SqlConnection _argoConnection;
        private ExchangeEntity _exchangeEntity;
        private DateTime _dateBeginPeriod;

        private byte[] _lastStamp;
        
        /// <summary>
        /// Ctor
        /// </summary>
        public TransferSheetExchangeTask()
        {
            _lastStamp = new byte[8];
            _dataTable = new DataTable();
            if (ConfigurationManager.AppSettings["DatePeriodBegin"] != null)
                _dateBeginPeriod = Convert.ToDateTime(ConfigurationManager.AppSettings["DatePeriodBegin"]);
            else
                _dateBeginPeriod = DateTime.Today;
        }

        /// <summary>
        /// Entity name
        /// </summary>
        public string EntityName
        {
            get { return "TransferSheet"; }
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
        /// <param name="argoConnection">Source's connetion string</param>
        /// <param name="exchangeEntity">Object of exchange with historic info (last timestamp)</param>
        public void PrepareArgoData(System.Data.SqlClient.SqlConnection argoConnection, ExchangeEntity exchangeEntity)
        {
            _argoConnection = argoConnection;
            _exchangeEntity = exchangeEntity;

            _lastStamp = exchangeEntity.LastState;

            PublishEventLog(ExchangeStatusType.Unknown, "Запрос данных \"Товарные накладные\" из АРГО", null);

            try
            {

                if (_argoConnection.State != ConnectionState.Open)
                    _argoConnection.Open();

                string cmdText = @"SELECT sheet.[UID]
                                     ,sheet.[Number]
                                     ,sheet.[DateCreate] as [DatePackage]
                                     ,(manager.Surname + SPACE(1) + SUBSTRING(manager.FirstName, 1, 1) + '.'+ SUBSTRING(manager.MiddleName, 1, 1) + '.') AS ManagerName
                                     ,(courier.Surname + SPACE(1) + SUBSTRING(courier.FirstName, 1, 1) + '.'+ SUBSTRING(courier.MiddleName, 1, 1) + '.') AS CourierName
                                     ,sheet.[DivisionUID]
                                     ,sheet.[Status]
                                     ,sheet.[Amount]
                                     ,sheet.[IsDeleted]
                                     ,sheet.[State]
                               FROM [dbo].[TransferSheet] sheet
                               JOIN [dbo].[Employee] manager ON sheet.[EmployeeUID] = manager.[UID]
                               JOIN [dbo].[Employee] courier ON sheet.[AdvanceHolderEmployeeUID] = courier.[UID]
                               WHERE sheet.[Status] IN (2,1) AND sheet.[State] > @State AND sheet.[DateCreate] >= @DatePeriodBegin";

                SqlCommand command = new SqlCommand(cmdText, argoConnection);
                command.Parameters.AddWithValue("@State", _exchangeEntity.LastState);
                command.Parameters.AddWithValue("@DatePeriodBegin", _dateBeginPeriod);

                SqlDataAdapter adapter = new SqlDataAdapter(command);

                _dataTable.Clear();

                adapter.Fill(_dataTable);

                PublishEventLog(ExchangeStatusType.Unknown,
                                string.Format("Подготовлено {0} записей для обновления", _dataTable.Rows.Count), null);
            }
            catch (Exception ex)
            {
                Log.Error("TransferSheetExchangeTask. PrepareArgoData Method Error. ",ex);
                throw;
            }
        }

        /// <summary>
        /// Method for saving data to destination
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        public void ApplyChanges(UnitOfWork unitOfWork)
        {
            var repository = unitOfWork.TransferSheetRepository;

            var transfersheets = repository.GetFullList();
            foreach (DataRow row in _dataTable.Rows)
            {
                byte[] lastStamp = (byte[])row["State"];
                if (new SqlBinary(lastStamp) > new SqlBinary(_lastStamp))
                    _lastStamp = lastStamp;

                bool isDeleted = Convert.ToBoolean(row["IsDeleted"]);

                var transferSheet = NewTransferSheet(row);

                var sheet = transfersheets.FirstOrDefault(x => x.UID == transferSheet.UID);

                try
                {

                    if (sheet != null)
                    {
                        if (sheet.TransferSheetStatusType == Model.TransferSheetStatusType.Moved)
                        {
                            if (isDeleted && !sheet.IsDeleted)
                            {
                                sheet.IsDeleted = true;
                                repository.Update(sheet);
                                repository.Save();
                            }
                            else
                            {
                                using (var t = new TransactionScope())
                                {
                                    sheet.IsDeleted = transferSheet.IsDeleted;
                                    sheet.AmountOfMoney = transferSheet.AmountOfMoney;
                                    sheet.CourierName = transferSheet.CourierName;
                                    sheet.ManagerName = transferSheet.ManagerName;
                                    sheet.DatePackage = transferSheet.DatePackage;
                                    sheet.Number = transferSheet.Number;

                                    sheet.JewelrySubjects.Clear();
                                    repository.Update(sheet);
                                    repository.Save();

                                    AddJewelrySubjects(sheet);
                                    repository.Update(sheet);
                                    repository.Save();

                                    t.Complete();
                                }
                            }
                            PublishEventLog(ExchangeStatusType.Update, "Успешное обновление", null);
                        }
                        else
                        {
                            PublishEventLog(ExchangeStatusType.Update, "Неудачное обновление",
                                            "Товарная накладная находится в статусе \"Принята\". Обновление невозможно.");
                        }
                    }
                    else
                    {
                        AddJewelrySubjects(transferSheet);
                        repository.Add(transferSheet);
                        repository.Save();
                        PublishEventLog(ExchangeStatusType.Insert, "Успешная вставка", null);
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("Error update",exception);
                    throw;
                }
            }
            PublishEventLog(ExchangeStatusType.Unknown, "Обновление данных завершено", null);
        }

        /// <summary>
        /// Method for loading additional data from source
        /// </summary>
        /// <param name="unitOfWork">Unit of work object for requesting data from source</param>
        private void AddJewelrySubjects(TransferSheet transferSheet)
        {
            if (_argoConnection.State != ConnectionState.Open)
                _argoConnection.Open();

            var cmdText = @"SELECT js.[UID]
                           ,sheet.[TransferSheetUID]
                           ,(div.Code+SPACE(1)+CAST(number.Number as nvarchar(20))) as TicketNumber
                           ,('от '+CONVERT(nvarchar(20), ticket.DateProcess, 102)+', кредит '+CAST(ticket.Credit as nvarchar(20)) +' на '+CAST(DATEDIFF(DAY,ticket.DateProcess, ticket.DateReturn)+1 as nvarchar(20)) +' дней') as TicketInfo
                           ,ticket.DateProcess
                           ,ticket.PercentRate
                           ,js.[JewelryTypeUID]
                           ,ins.InsertDesc as DiamondInsert
                           ,js.[JewelryProofUID]
                           ,js.[Weight]
                           ,(js.[Weight] - ISNULL(insWeights.InsWeight,0)) as [MetalWeight]
	                       ,subj.[Cost] as EstimateCost
	                       ,ROUND(ticket.[Credit]*subj.[Cost]/ISNULL(tcost.[TicketCost],ticket.[Credit]), 2) as Credit
                           ,(emp.Surname + SPACE(1) + SUBSTRING(emp.FirstName, 1, 1) + '.'+ SUBSTRING(emp.MiddleName, 1, 1) + '.') AS TicketManagerName
                           ,cat.Name as JewelryCategoryName
                           ,cat.Code as JewelryCategoryCode
                           ,js.[IsDeleted]
                           ,js.[State]
                        FROM [dbo].[JewelrySubject] js 
                        JOIN [dbo].[JewelryCategory] cat ON js.JewelryCategoryUID = cat.UID
                        JOIN [dbo].[Subject] subj ON js.[UID] = subj.[UID]
                        LEFT JOIN (SELECT [SubjectUID],
			                    STUFF(
			                    (
				                    SELECT ','+ [Name] FROM [JewelryInsert] WHERE [SubjectUID] = t.[SubjectUID] FOR XML PATH('')
			                    ),1,1,'') as InsertDesc
		                    FROM (SELECT DISTINCT [SubjectUID] FROM [JewelryInsert] WHERE NOT JewelryStoneUID IS NULL ) t
	                    ) ins ON subj.UID = ins.SubjectUID
                        LEFT JOIN (SELECT [SubjectUID], SUM(Weight) as InsWeight FROM JewelryInsert GROUP BY [SubjectUID]) insWeights ON subj.UID = insWeights.SubjectUID
                        LEFT JOIN (SELECT [TicketUID], SUM([Cost]) as TicketCost FROM [Subject] GROUP BY [TicketUID]) tcost ON tcost.[TicketUID] = subj.[TicketUID]
                        JOIN [dbo].[JewelryProof] proof ON js.[JewelryProofUID] = proof.[UID]
                        JOIN [dbo].[JewelryType] jtype ON js.[JewelryTypeUID] = jtype.[UID]
                        JOIN [dbo].[Ticket] ticket ON subj.[TicketUID] = ticket.[UID] 
                        JOIN [dbo].[Employee] emp ON ticket.[EmployeeUID] = emp.[UID] 
                        JOIN [dbo].[TicketNumber] number ON ticket.[TicketNumberUID] = number.[UID] 
                        JOIN [dbo].[Division] div ON ticket.[DivisionUID] = div.[UID] 
                        JOIN [dbo].[TransferSheet2Ticket] sheet ON sheet.[TicketUID] = ticket.[UID]
                        WHERE sheet.[TransferSheetUID] = @TransferSheetUID
		                    AND ticket.IsDeleted = 0 
		                    AND subj.IsDeleted = 0 
		                    AND sheet.IsDeleted = 0 
		                    AND js.IsDeleted = 0";

            SqlCommand cmd = new SqlCommand(cmdText, _argoConnection);
            cmd.Parameters.AddWithValue("@TransferSheetUID", transferSheet.UID);
            SqlDataAdapter adapter = new SqlDataAdapter(cmd);

            DataTable dataTable = new DataTable();
            adapter.Fill(dataTable);
            
            foreach (DataRow row in dataTable.Rows)
                transferSheet.JewelrySubjects.Add
                    (NewJewelrySubject(row));
        }

        /// <summary>
        /// Method for creating new JewelrySubject from DataRow 
        /// </summary>
        /// <param name="row">Row contains data</param>
        /// <returns>The new JewelrySubject object</returns>
        private JewelrySubject NewJewelrySubject(DataRow row)
        {
            JewelrySubject subject = new JewelrySubject();
            subject.UID = Guid.Parse(row["UID"].ToString());
            subject.TransferSheetUID = Guid.Parse(row["TransferSheetUID"].ToString());

            if(!row.IsNull("Credit"))
                subject.Credit = Convert.ToDecimal(row["Credit"]);
            if (!row.IsNull("DiamondInsert"))
                subject.DiamondInsert = row["DiamondInsert"].ToString();
            if (!row.IsNull("EstimateCost"))
                subject.EstimateCost = Convert.ToDecimal(row["EstimateCost"]);
            
            subject.IsDeleted = Convert.ToBoolean(row["IsDeleted"]);
            
            subject.JewelryProofUID = Guid.Parse(row["JewelryProofUID"].ToString());
            subject.JewelryTypeUID = Guid.Parse(row["JewelryTypeUID"].ToString());
            if (!row.IsNull("MetalWeight"))
                subject.MetalWeight = Convert.ToDecimal(row["MetalWeight"]);
            if (!row.IsNull("Weight"))
                subject.Weight = Convert.ToDecimal(row["Weight"]);

            subject.TicketInfo = row["TicketInfo"].ToString();
            subject.TicketNumber = row["TicketNumber"].ToString();
            subject.TicketManagerName = row["TicketManagerName"].ToString();

            if (!row.IsNull("DateProcess"))
                subject.DateProcess = Convert.ToDateTime(row["DateProcess"]);

            if (!row.IsNull("PercentRate"))
                subject.PercentRate = Convert.ToDecimal(row["PercentRate"]);

            if (!row.IsNull("JewelryCategoryName"))
                subject.JewelryCategoryName = row["JewelryCategoryName"].ToString();
            if (!row.IsNull("JewelryCategoryCode"))
                subject.JewelryCategoryCode = row["JewelryCategoryCode"].ToString();

            return subject;
        }

        /// <summary>
        /// Method for creating new TransferSheet from DataRow 
        /// </summary>
        /// <param name="row">Row contains data</param>
        /// <returns>The new TransferSheet object</returns>
        private TransferSheet NewTransferSheet(DataRow row)
        {
            TransferSheet sheet = new TransferSheet();
            sheet.UID = Guid.Parse(row["UID"].ToString());
            if(!row.IsNull("Amount"))
                sheet.AmountOfMoney = decimal.Parse(row["Amount"].ToString());
            sheet.CourierName = row["CourierName"].ToString();
            if (!row.IsNull("DatePackage"))
                sheet.DatePackage = DateTime.Parse(row["DatePackage"].ToString());
            sheet.ManagerName = row["ManagerName"].ToString();
            sheet.Number = row["Number"].ToString();
            sheet.TransferSheetStatusType = Model.TransferSheetStatusType.Moved;
            sheet.DivisionUID = Guid.Parse(row["DivisionUID"].ToString());

            return sheet;
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
