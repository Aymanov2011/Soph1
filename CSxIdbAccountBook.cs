// -----------------------------------------------------------------------
//  <copyright file="CSxIdbAccountBook.cs" company="Sophis Tech (Toolkit)">
//  Copyright (c) Sophis Tech. All rights reserved.
//  </copyright>
//  <author>Marco Cordeiro</author>
//  <created>2013/07/30</created>
// -----------------------------------------------------------------------

namespace IDB.BackOffice.ISDB.Accounting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Data;
    using IslamicInstruments;
    using NSREnums;
    using sophis.instrument;
    using sophis.misc;
    using sophis.portfolio;
    using Sophis.Toolkit.Utils.Cache;
    using sophis.accounting;
    using Sophis.Toolkit.Utils.Log;
    using sophis.value;
    using System.Windows.Forms;
    using sophis.utils;
    using sophis.backoffice_kernel;
    using Oracle.DataAccess.Client;
     using Sophis.DataAccess;
  

    public class CSxIdbAccountBook : CSMAccPostingColumn
    {
        private IList<int> _accountExceptions;
        public static  string IDBGroupAccountEntity="IDB Group Funds";
        public static Dictionary<int, int> fNostroAndEntity = new Dictionary<int, int>();     
        public static bool fDataLoaded =false;
        public IList<int> AccountExceptions
        {
            get
            {
                if (_accountExceptions != null) return _accountExceptions;
                try
                {
                    string value = null;
                    CSMConfigurationFile.getEntryValue("", "", ref value, "33528");
                    var split = value.Split(new[] {',', ';'});
                    _accountExceptions = split.Select(x => Convert.ToInt32(x)).ToList();
                }
                catch (Exception)
                {
                    _accountExceptions = new List<int> {33528};
                }
                return _accountExceptions;
            }
        }
        public string fModelName = "";

        public override void GetCell(CSMAccPostingData data, ref SSMCellValue value, SSMCellStyle style)
        {
            using (var log = LogHelper.GetLogger(GetType().Name, MethodBase.GetCurrentMethod().Name))
            {
                style.kind = eMDataType.M_dNullTerminatedString;

                var accountData = data.GetAccPosting();
                var tradeId = accountData.fTradeID;
                 var accountId=0;

               
              
                SSMFinalPosting  _fPosting=data.GetAccPosting();
                if (_fPosting != null)
                {
                    accountId = _fPosting.fAccountNameID;
                    if (_fPosting.fPostingType == 11)
                    {
                        CSMPosition pos = CSMPosition.GetCSRPosition(_fPosting.fPositionID);
                        if (pos != null)
                        {
                            CSMPortfolio port = pos.GetPortfolio();
                            if (port != null)
                            {
                                CSMAmFund fund = CSMAmFund.GetFundFromFolio(port);
                                if (fund != null)
                                {
                                    string accountBookName = GetAccountBookNameForFund(fund);
                                    value.SetString(accountBookName);
                                    return;
                                }
                            }
                        }
                        return;
                    }
                }

                if (tradeId == 0 || accountId==0)
                    return;

                CSMTransaction trade = CSMTransaction.newCSRTransaction(tradeId);
                if (trade == null)
                    return;
               
                //Business Event
                int type = (int)trade.GetTransactionType();

                //Any Posting linked to the IDB Group should have account book 1000
                if (_fPosting.fAccountNameID == 33528)
                {
                    value.SetString(GetNostroIDBAccountBook(_fPosting.fID));
                    return;
                }
                int PostingId = _fPosting.fID;
                int NostroId = _fPosting.fNostroAccountID;
                if (NostroId > 0)
                {
                    int EntityId = 0; 
                    if (fDataLoaded == false)
                        LoadAccountEntities();
                    fNostroAndEntity.TryGetValue(NostroId, out  EntityId);                   
                    if (IsIDBGroupEntity(EntityId))
                    {
                        value.SetString("1000");
                        return;
                    }
                }               

                var cacheKey = string.Format("AccountBook_{0}", accountData.fID);
                var strValue = CacheManager.GetItem<string>(cacheKey);

                try
                {
                  

                    if (!string.IsNullOrEmpty(strValue))
                    {
                        value.SetString(strValue);
                        return;
                    }

                    //if value is not cached already we loaded it
                    strValue = CSxDataFacade.GetAccountBookName(accountData.fAccountingBookID);
                  
                    

                    //Filter on the account defined in the config file
                    if (CSxIslamicInstrumentsHelperCLI.IsForbiddenAccountId(accountId))
                    { 
                        var parentId = 0;
                        try
                        {
                            //parentId = CSxIslamicInstrumentsHelperCLI.GetParentTrade(tradeId);
                          
                           //couponS and redemption sukuk are not taken in account in this function
                            parentId = CSxIdbTradeId.GetParentIDBMergingAllocatedTrade(type, tradeId);
                        }
                        catch (Exception)
                        {
                            log.WriteSystemError("Failed to check if tarde is merged");
                        }
                        // AccountExceptions.Contains(accountData.fAccountingBookID)
                        //FILTER on merged trades  
                        if (parentId != 0  )
                        {
                            try
                            {
                                var t = CSMTransaction.newCSRTransaction(tradeId);
                                if (t == null)
                                    throw new Exception("Failed to open trade");

                                var fid = 0;
                                //if(CSxIslamicInstrumentsHelperCLI.IsInterFundCashTransferBusinessEvent(type))
                                //{
                                //    var tr = CSMTransaction.newCSRTransaction(parentId);
                                //    if (tr == null)
                                //        throw new Exception("Failed to open trade");

                                //    fid = CSxIslamicInstrumentsHelperCLI.GetFundForEntity(tr.GetEntity());
                                //}
                                //else
                                //{
                                 fid =    CSxIslamicInstrumentsHelperCLI.GetFundForEntity(t.GetEntity());                                
                                //}


                                CSMAmFund f = CSMInstrument.GetInstance(fid);
                                if (f == null)
                                    throw new Exception("Failed to find fund for settlement entity");
                                strValue = CSxDataFacade.GetAccountBookIdForFolio(f.GetTradingPortfolio());


                            }
                            catch (Exception e)
                            {
                                log.WriteSystemError("Failed to get Account Book Id from Settlement Entity for merged trade {0} : {1}", tradeId, e.Message);

                            }
                        }
                        //if (strValue != null)
                        //{
                        //    //strValue = strValue.Substring(0, Math.Min(4, strValue.Length));
                        //    //we cache the value for next time
                        //    CacheManager.SetItem(cacheKey, strValue);

                        //}
                    
               }

                    if (strValue != null)
                    {
                        strValue = strValue.Substring(0, Math.Min(4, strValue.Length));
                        value.SetString(strValue);
                    }
                      


                }
                catch (Exception e)
                {
                    log.WriteSystemError("Failed to get Account Book Name for trade {0} : {1}", tradeId, e.Message);
                   
                    if (IDB.IslamicInstruments.CSxIslamicInstrumentsHelperCLI.IsInGUIMode())
                    {
                        MessageBox.Show("Failed to get Account Book Name for trade " + tradeId+" Error:"+ e.Message);
                    }
                }
            }
        }

        public string GetAccountBookNameForFund(CSMAmFund fund)
        {
            string result = "";
            int fundFolioCode = fund.GetTradingPortfolio();
            string SQLQuery = "select substr(b.name, 1,4) IDB_ACCOUNT_BOOK from account_book_folio bf , account_book b where b.ID=bf.ACCOUNT_BOOK_ID and bf.FOLIO_ID = " + fundFolioCode + " and record_type=1";
            using (OracleCommand myCommand = new OracleCommand(SQLQuery, DBContext.Connection))
            {
                using (OracleDataReader myReader = myCommand.ExecuteReader())
                {
                    while (myReader.Read())
                    {
                        result = myReader["IDB_ACCOUNT_BOOK"].ToString();
                        break;
                    }
                }
            }
            return result;
        }


        //public void LoadAccountEntities()
        //{
        //    string SQLQuery = "select ID  from ACCOUNT_ENTITY where Name = '" + IDBGroupAccountEntity + "' and record_type = 1";
        //    using (OracleCommand myCommand = new OracleCommand(SQLQuery, DBContext.Connection))
        //    {
        //        using (OracleDataReader myReader = myCommand.ExecuteReader())
        //        {
        //            while (myReader.Read())
        //            {                        
        //                int.TryParse(myReader["ID"].ToString(), out IDBGroupAccountEntityID);                      

        //            }
        //        }
        //    }




        //}

        public void LoadAccountEntities()
        {
            int id = 0;
            int entity = 0;
            fNostroAndEntity.Clear(); 
            string SQLQuery = "select ID , ENTITY from BO_TREASURY_ACCOUNT ";
            using (OracleCommand myCommand = new OracleCommand(SQLQuery, DBContext.Connection))
            {
                using (OracleDataReader myReader = myCommand.ExecuteReader())
                {
                    while (myReader.Read())
                    {
                        int.TryParse(myReader["ID"].ToString(), out id);
                        int.TryParse(myReader["ENTITY"].ToString(), out entity);
                        if (!fNostroAndEntity.ContainsKey(id))
                            fNostroAndEntity.Add(id,entity);

                    }
                }
            }
            fDataLoaded = true;




        }
        bool IsIDBGroupEntity(int id)
        {

            CSMThirdParty entity = CSMThirdParty.GetCSRThirdParty(id);
            if (entity == null)
            {
                return false;
            }
            CMString reference=new CMString();
            entity.GetReference(reference);
            if (IDBGroupAccountEntity.CompareTo(reference.ToString())==0 )
            {
                return true;                
            }
            //reference.SetString("");

            //CSMThirdParty parent = entity.GetParent();
            //parent.GetReference(reference);
            //if (parent == null)
            //{
            //    return false;
            //}
            //if (IDBGroupAccountEntity.CompareTo(reference.ToString()) == 0)
            //{
            //    return true;
            //}

            return false;
        }

        public string GetNostroIDBAccountBook(int postingId)
        {
            string result = "1000";
            string SQLQuery = "select  substr(b.name, 1,4) IDB_ACCOUNT_BOOK from account_posting p, bo_treasury_account ta , folio f, account_book_folio bf, account_book b where  b.id=bf.account_book_id and f.IDENT = bf.FOLIO_ID and  f.ENTITE=ta.ENTITY and ta.id = p.NOSTRO_ID and p.account_name_id=33528 and b.RECORD_TYPE =1 and p.id=" + postingId;
            using (OracleCommand myCommand = new OracleCommand(SQLQuery, DBContext.Connection))
            {
                using (OracleDataReader myReader = myCommand.ExecuteReader())
                {
                    while (myReader.Read())
                    {
                        result = myReader["IDB_ACCOUNT_BOOK"].ToString();
                        break;
                    }
                }
            }

            return result;
        }


      


      
    }
}
