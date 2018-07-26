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
	public static Dictionary<int, int> fNostroAndBook = new Dictionary<int, int>(), fGLAndBook = new Dictionary<int, int>();     
	public static bool fDataLoaded =false, fGLDataLoaded =false;

	public override void GetCell(CSMAccpostingData data, ref SSMCellValue value, SSMCellStyle style)
	{
		var postingData = data.GetAccPosting();
		int tradeId = 0, accountNameId=0;
		int REVALUATION = 11, IDB_NOSTRO = 33528;
		String strValue = "Could Not Get Value",tempString;

		style.kind = eMDataType.M_dNullTerminatedString;

		if (postingData != null)
		{
			if (postingData.fPostingType == REVALUATION)
			{
				CSMPosition pos = CSMPosition.GetCSRPosition(postingData.fPositionID);
				if (pos != null)
				{
					CSMPortfolio port = pos.GetPortfolio();
					if (port != null)
					{
						CSMAmFund fund = CSMAmFund.GetFundFromFolio(port);  //GetHedgeFund()
						if (fund != null)
							strValue = GetAccountBookNameForFund(fund);
					}
				}
			}else{			
				accountNameId = postingData.fAccountNameID;
				tradeId = postingData.fTradeID; 
				
				if (tradeId != 0 && accountNameId != 0)
				{
					if (accountNameId == IDB_NOSTRO)
						strValue = GetNostroIDBAccountBook(postingData.fNostroAccountID, postingData.fAccountNumber);
					else
					{
						tempString = CSxDataFacade.GetAccountBookName(postingData.fAccountingBookID);
						if (tempString != null)
							strValue = tempString.Substring(0, Math.Min(4, tempString.Length));
					}
				}
			}
		}
		value.SetString(strValue);
	}
	
	public string GetAccountBookNameForFund(CSMAmFund fund)
	{
		string result = null;
		int fundFolioCode = fund.GetTradingPortfolio();
		string SQLQuery = "select SICOVAM ID, substr(b.name, 1,4) IDB_ACCOUNT_BOOK from FUNDS f, account_book_folio bf , account_book b where b.ID=bf.ACCOUNT_BOOK_ID and record_type=1 and f.TRADINGFOLIO=bf.FOLIO_ID  AND f.FUNDTYPE=1";
		
		using (OracleCommand myCommand = new OracleCommand(SQLQuery, DBContext.Connection))
		{
			using (OracleDataReader myReader = myCommand.ExecuteReader())
			{
				if (myReader.Read())
					result = myReader["IDB_ACCOUNT_BOOK"].ToString();
			}
		}
		return result;
	}

	public string GetNostroIDBAccountBook(int NostroId, int GLId)
	{
		string result = null;
		if(NostroId!=0)
		{
			if(fDataLoaded==false)
				LoadNostroAccountBook();
			fNostroAndBook.TryGetValue(NostroId, out  result);
		}else{
			if(fGLDataLoaded==false)
				LoadGLAccountBook();
			fGLAndBook.TryGetValue(GLId, out  result);
		}

		return result;
	}

	public void LoadNostroAccountBook()
	{
		int id = 0, accountBook = 0;
		fNostroAndBook.Clear(); 
		string SQLQuery = "select  ta.id ID, substr(b.name, 1,4) IDB_ACCOUNT_BOOK from bo_treasury_account ta , folio f, account_book_folio bf, account_book b where  b.id=bf.account_book_id and f.IDENT = bf.FOLIO_ID and  f.ENTITE=ta.ENTITY and b.RECORD_TYPE =1";
		
		using (OracleCommand myCommand = new OracleCommand(SQLQuery, DBContext.Connection))
		{
			using (OracleDataReader myReader = myCommand.ExecuteReader())
			{
				while (myReader.Read())
				{
					int.TryParse(myReader["ID"].ToString(), out id);
					int.TryParse(myReader["IDB_ACCOUNT_BOOK"].ToString(), out accountBook);
					if (!fNostroAndBook.ContainsKey(id))
						fNostroAndBook.Add(id,accountBook);

				}
			}
		}
		fDataLoaded = true;
	}

	public void LoadGLAccountBook()
	{
		int id = 0, ccountBook = 0;
		fGLAndBook.Clear(); 
		string SQLQuery = "select  ACCOUNT_NUMBER ID, substr(b.name, 1,4) IDB_ACCOUNT_BOOK from bo_treasury_account ta , folio f, account_book_folio bf, account_book b, ACCOUNT_MAP m where  b.id=bf.account_book_id and f.IDENT = bf.FOLIO_ID and  f.ENTITE=ta.ENTITY and b.RECORD_TYPE =1 and ta.id = m.NOSTRO_ID and m.ACCOUNT_NAME_ID=33528";

		using (OracleCommand myCommand = new OracleCommand(SQLQuery, DBContext.Connection))
		{
			using (OracleDataReader myReader = myCommand.ExecuteReader())
			{
				while (myReader.Read())
				{
					int.TryParse(myReader["ID"].ToString(), out id);
					int.TryParse(myReader["IDB_ACCOUNT_BOOK"].ToString(), out accountBook);
					if (!fGLAndBook.ContainsKey(id))
						fGLAndBook.Add(id,accountBook);

				}
			}
		}
		fGLDataLoaded = true;
	}
}