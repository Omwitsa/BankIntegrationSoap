Imports System.Web.Services
Imports System.ComponentModel
Imports System
Imports System.IO
Imports System.Data.SqlClient
Imports log4net

' To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line.
' <System.Web.Script.Services.ScriptService()> _
<WebService(Namespace:="unisol:bankapi")>
<WebServiceBinding(ConformsTo:=WsiProfiles.BasicProfile1_1)>
<ToolboxItem(False)>
Public Class ABNUnisolBankService
	Inherits WebService

	Public strSQL As String
	Public strDataBaseType As String = ""
	Public strDataSource As String = ""
	Public strCatalog As String = ""
	Public pwd As String = "Ôçâ$èå!÷"
	Public usr As String = "abnunisol"

	Private myConn As SqlConnection
	Private myCmd As SqlCommand
	Private myReader As SqlDataReader
	Private ReadOnly Logger As ILog = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType)

	<WebMethod>
	Private Function Decode(ByVal icText As String) As String
		Dim icLen As Integer
		Dim i As Integer
		Dim icNewText As String = ""
		Dim icChar As String

		On Error Resume Next

		icLen = Len(icText)
		For i = 1 To icLen
			icChar = Mid(icText, i, 1)
			Dim x As String = Asc(icChar)
			Select Case x
				Case 192 To 217
					icChar = Chr(x - 127)
				Case 218 To 243
					icChar = Chr(x - 121)
				Case 244 To 253
					icChar = Chr(x - 196)
				Case 32
					icChar = Chr(32)
			End Select
			icNewText = icNewText + icChar
		Next
		Return icNewText
	End Function

	<WebMethod>
	Private Function Encode(ByVal icText As String) As String
		Dim icLen As Integer
		Dim i As Integer
		Dim icNewText As String = ""
		Dim icChar As String

		On Error Resume Next

		icLen = Len(icText)
		For i = 1 To icLen
			icChar = Mid(icText, i, 1)
			Select Case Asc(icChar)
				Case 65 To 90
					icChar = Chr(Asc(icChar) + 127)
				Case 97 To 122
					icChar = Chr(Asc(icChar) + 121)
				Case 48 To 57
					icChar = Chr(Asc(icChar) + 196)
				Case 32
					icChar = Chr(32)
			End Select
			icNewText = icNewText + icChar
		Next
		Return icNewText
	End Function

	<WebMethod>
	Private Function GetConnectionString() As String
		Dim sr As New StreamReader(Server.MapPath("DBserver.dat"))
		'Read db details
		strDataBaseType = sr.ReadLine()
		strDataSource = sr.ReadLine()
		strCatalog = sr.ReadLine()

		Dim conn As String = "data source=" + strDataSource + ";" +
			"initial catalog=" + strCatalog + ";" +
			"integrated security=False;" +
			"MultipleActiveResultSets=True;" +
			"User ID=" + usr + ";" +
			"Password=" + Decode(Encode(pwd)) + ";" +
			"App=EntityFramework"

		Dim conn1 As String = "data source=" + strDataSource + ";" +
							  "initial catalog=" + strCatalog + ";" +
							  "integrated security=False;" +
							  "MultipleActiveResultSets=True;" +
							  "User ID=portal;" +
							  "Password=p0t@l2.14;" +
							  "App=EntityFramework"
		Return conn

	End Function

	<WebMethod>
	Public Function IsThisBonaFideStudent(ByVal strRegNo As String,
										  ByVal strUN As String, ByVal strPWD As String) As CheckStudentResponse
		On Error Resume Next
		Logger.Info("IsThisBonafideStudent :: strRegNo = " & strRegNo & ",strUN=" & strUN)
		Dim strValue = ""
		Dim results = ""
		Dim strType = ""
		Dim strAdmnNo = ""

		Dim res = New CheckStudentResponse With {
			.strRegNo = strRegNo,
			.strUN = strUN
		}
		Dim conn As String = GetConnectionString()
		myConn = New SqlConnection(conn)
		myConn.Open()

		Dim uName = Replace(strUN, "'", "''")
		Dim pwd = Replace(strPWD, "'", "''")
		Dim admNo = Replace(strRegNo, "'", "''")

		If uName = "" Or pwd = "" Or admNo = "" Then
			myReader.Close()
			res.strStatus = "FAIL"
			res.strMsg = "INVALID PAYLOAD"
			Logger.Info("IsThisBonafideStudentResponse :: strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg)
			Return res
		End If

		strSQL = "SELECT names FROM BankAcc WHERE [UserName] = @uName AND [Password] = @pwd "
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.Parameters.AddWithValue("uName", uName)
		myCmd.Parameters.AddWithValue("pwd", Encode(pwd))
		myReader = myCmd.ExecuteReader()

		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				results = myReader.GetString(0)
			End If
			myReader.Close()
			Exit While
		End While
		If results = "" Then
			res.strStatus = "FAIL"
			res.strMsg = "AUTHENTICATION FAILED"
			Logger.Info("IsThisBonafideStudentResponse :: strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg)
			Return res
		End If

		strSQL = "SELECT [Names] FROM Register WHERE [AdmnNo] = @admNo;"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.Parameters.AddWithValue("admNo", admNo)
		myReader = myCmd.ExecuteReader()
		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				strValue = myReader.GetString(0)
				strType = "STUDENT"
			End If
			myReader.Close()
			Exit While
		End While

		If strValue = "" Then
			strSQL = "SELECT [Names],[AdmnNo] FROM Applicant WHERE [Ref]= @admNo OR [AdmnNo] = @admNo"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.Parameters.AddWithValue("admNo", admNo)
			myReader = myCmd.ExecuteReader()
			While myReader.Read()
				If Not myReader.IsDBNull(0) Then
					strValue = myReader.GetString(0)
					strAdmnNo = myReader.GetString(1)
					strType = "APPLICANT"
				End If
				myReader.Close()
				Exit While
			End While
		End If
		myConn.Close()

		If strAdmnNo IsNot "" Then
			strType = "APPLICANT-KUCCPS"
		End If

		If strValue = "" Then
			res.strStatus = "FAIL"
			res.strMsg = "NOT A STUDENT"
		Else
			res.strStatus = "OK"
			res.strStudName = UCase(strValue)
			res.strMsg = "SUCCESS"
		End If
		Logger.Info("IsThisBonafideStudentResponse :: strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg & " ,strType=" & strType)
		Return res
	End Function

	<WebMethod>
	Public Function ProcessStudentFees(ByVal strRegNo As String, ByVal dblAmount As Double, ByVal strTransNo As String,
									   ByVal dtTransDate As Date, ByVal strUN As String, ByVal strPWD As String) As FeeProcessResponse
		On Error Resume Next
		Logger.Info("ProcessStudentFees :: " & strRegNo & ", dblAmount=" & dblAmount & ", strTransNo=" & strTransNo & ", strUN=" & strUN & ", strTransDate=" & dtTransDate)
		Dim boolSuccessfullyAuthenticated As Boolean = False
		Dim strRcptNo As String = ""
		Dim strStudNames As String = ""
		Dim strLedger As String = ""
		Dim strPayMode As String = ""
		Dim strProj As String = ""
		Dim strCur As String = ""
		Dim boolAlreadyCaptured As Boolean = False
		Dim sNumber As Single
		Dim dtDate As Date = DateTime.Now
		Dim dblSpentAmount As Double
		Dim dblInitAmount As Double
		Dim dblReqAmount() As Double
		Dim dblPaidAmount() As Double
		Dim dblInsertAmount() As Double
		Dim booPaidAmtRead() As Boolean
		Dim dblCredit As Double
		Dim strRCreditDebitAcc As String = ""
		Dim boolSkipFeesBreakDown As Boolean = False
		Dim lAccs As Long
		Dim i As Long
		Dim y As Long
		Dim listAccs As New List(Of String)
		Dim strType = ""
		Dim strAppCampus = ""
		Dim strAppProg = ""
		Dim strAdmnNo = ""

		Dim res = New FeeProcessResponse With {
			.strRegNo = strRegNo,
			.strUN = strUN,
			.strRcptNo = strRcptNo,
			.strTransNo = strTransNo
		}

		Dim conn As String = GetConnectionString()
		myConn = New SqlConnection(conn)
		myConn.Open()

		Dim uName = Replace(strUN, "'", "''")
		Dim pwd = Replace(strPWD, "'", "''")
		Dim admNo = Replace(strRegNo, "'", "''")
		Dim modeNo = Replace(strTransNo, "'", "''")

		If admNo = "" Or dblAmount <= 0 Or modeNo = "" Then
			myConn.Close()
			res.strStatus = "FAIL"
			res.strMsg = "INVALID PAYLOAD"
			Logger.Info("ProcessStudentFeesResponse :: strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg)
			Return res
		End If

		If uName <> "" And pwd <> "" Then
			strSQL = "SELECT BankAcc.Ledger,BankAcc.[names] As [PayMode],Ledger.[Curr] 
                    FROM BankAcc,Ledger WHERE BankAcc.Ledger=Ledger.[Names] 
                    AND BankAcc.[UserName] = @uName 
                    AND BankAcc.[Password] = @pwd;"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.Parameters.AddWithValue("uName", uName)
			myCmd.Parameters.AddWithValue("pwd", Encode(pwd))
			myReader = myCmd.ExecuteReader()
			Do While myReader.Read()
				If Not myReader.IsDBNull(0) Then '
					boolSuccessfullyAuthenticated = True
					strLedger = myReader.GetString(0)
					strPayMode = myReader.GetString(1)
					strCur = myReader.GetString(2)
					strProj = "<NONE>"
				End If
			Loop
			myReader.Close()
		End If

		If Not boolSuccessfullyAuthenticated Or strLedger = "" Then
			myConn.Close()
			res.strStatus = "FAIL"
			res.strMsg = "AUTHENTICATION FAILED"
			Logger.Info("ProcessStudentFeesResponse :: strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg)
			Return res
		End If

		strSQL = "SELECT Register.[Names] 
                    FROM register WHERE [AdmnNo] = @admNo"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.Parameters.AddWithValue("admNo", admNo)
		myReader = myCmd.ExecuteReader()
		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				Dim names = myReader.GetString(0)
				strStudNames = UCase(names)
				strType = "STUDENT"
			End If
			myReader.Close()
			Exit While
		End While

		If strStudNames = "" Then
			strSQL = "SELECT [Names],[Programme],[Campus],[AdmnNo]
                        FROM Applicant WHERE [Ref]= @admNo  OR [AdmnNo] = @admNo"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.Parameters.AddWithValue("admNo", admNo)
			myReader = myCmd.ExecuteReader()
			While myReader.Read()
				If Not myReader.IsDBNull(0) Then
					Dim names = myReader.GetString(0)
					strStudNames = UCase(names)
					strAppProg = myReader.GetString(1)
					strAppCampus = myReader.GetString(2)
					strAdmnNo = myReader.GetString(3)
					strType = "APPLICANT"
				End If
				myReader.Close()
				Exit While
			End While
		End If

		If strStudNames = "" Then
			myConn.Close()
			res.strStatus = "FAIL"
			res.strMsg = "STUDENT VALIDATION FAILED"
			Logger.Info("ProcessStudentFeesResponse :: strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg & " ,strType=" & strType)
			Return res
		End If

		If strAdmnNo IsNot "" Then
			strType = "APPLICANT-KUCCPS"
		End If

		strSQL = "SELECT [Receipt Number] 
                    FROM ReceiptBook WHERE AdmnNo LIKE @admNo AND Amount = @amt AND [mode number] LIKE @modeNo"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.Parameters.AddWithValue("admNo", admNo)
		myCmd.Parameters.AddWithValue("amt", dblAmount)
		myCmd.Parameters.AddWithValue("modeNo", modeNo)
		myReader = myCmd.ExecuteReader()

		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				boolAlreadyCaptured = True
			End If
			myReader.Close()
			Exit While
		End While

		If boolAlreadyCaptured Then
			myConn.Close()
			res.strStatus = "FAIL"
			res.strMsg = "ALREADY RECEIPTED"
			Logger.Info("ProcessStudentFeesResponse :: strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg)
			Return res
		End If

		strSQL = "SELECT RCreditAcc,SkipFeesBreakDown FROM SysSetup"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL

		myReader = myCmd.ExecuteReader()

		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				strRCreditDebitAcc = myReader.GetString(0)
				boolSkipFeesBreakDown = myReader.GetBoolean(1)
			End If
			myReader.Close()
			Exit While
		End While

		If strRCreditDebitAcc = "" Then
			myConn.Close()
			res.strStatus = "FAIL"
			res.strMsg = "INTERNAL FAIL"

			Dim resFail = "strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg & "-No strRCreditDebitAcc ,strType=" & strType
			Logger.Info("ProcessStudentFeesResponse :: " & resFail)
			Return res
		End If

		strSQL = "SELECT MAX(CAST ([Receipt Number] AS Real)) as NextRcpt FROM Receiptbook"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL

		myReader = myCmd.ExecuteReader()
		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				sNumber = myReader.GetSqlSingle(0)
			End If
			myReader.Close()
			Exit While
		End While

		strRcptNo = Format(sNumber + 1, "0000")
		If strRcptNo = "" Then
			myConn.Close()
			res.strStatus = "FAIL"
			res.strMsg = "INTERNAL FAIL"

			Dim resFail = "strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg & "- Next Receipt Failed ,strType=" & strType
			Logger.Info("ProcessStudentFeesResponse :: " & resFail)
			Return res
		End If

		strSQL = "INSERT INTO ReceiptBook ([Receipt Number],AdmnNo,[names],Amount,Curr,Credit,
                                            ledger,Project,[payment mode],[mode number],personnel,rdate,notes,Rtime,DocDate)
                    VALUES(@Receipt_Number,@AdmnNo,@names,@Amount,@Curr,@Credit,@ledger,
                            @Project,@payment_mode,@mode_number,@personnel,@rdate,@notes,@Rtime,@DocDate);"

		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.CommandType = CommandType.Text
		myCmd.Parameters.AddWithValue("Receipt_Number", strRcptNo)
		myCmd.Parameters.AddWithValue("AdmnNo", strRegNo)
		myCmd.Parameters.AddWithValue("names", strStudNames)
		myCmd.Parameters.AddWithValue("Amount", dblAmount)
		myCmd.Parameters.AddWithValue("Curr", strCur)
		myCmd.Parameters.AddWithValue("Credit", dblCredit)
		myCmd.Parameters.AddWithValue("ledger", strLedger)
		myCmd.Parameters.AddWithValue("Project", strProj)
		myCmd.Parameters.AddWithValue("payment_mode", strPayMode)
		myCmd.Parameters.AddWithValue("mode_number", UCase(strTransNo))
		myCmd.Parameters.AddWithValue("personnel", strUN)
		myCmd.Parameters.AddWithValue("rdate", DateTime.Now.Date)
		myCmd.Parameters.AddWithValue("notes", "Auto Receipted")
		myCmd.Parameters.AddWithValue("Rtime", DateTime.Now)
		myCmd.Parameters.AddWithValue("DocDate", Convert.ToDateTime(dtTransDate))

		Dim rowsAffected As Integer = myCmd.ExecuteNonQuery()
		Console.WriteLine("RowsAffected: {0}", rowsAffected)

		If strType = "APPLICANT" Then

			ProcessApplicantFees(strRcptNo, strAppCampus, strAppProg, dblAmount)
			res.strStatus = "OK"
			res.strRcptNo = strRcptNo
			res.strMsg = "SUCCESS"
			Dim resSkipBrk = " strRegNo=" & strRegNo & ",  strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg & " ,strType=" & strType & " ,strRcptNo=" & strRcptNo & ""
			Logger.Info("ProcessApplicantFeesResponse :: " & resSkipBrk)
			Return res
		End If

		If boolSkipFeesBreakDown Then
			myConn.Close()
			res.strStatus = "OK"
			res.strRcptNo = strRcptNo
			res.strMsg = "SUCCESS"

			Dim resSkipBrk = " strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg & " ,strType=" & strType & " ,strRcptNo=" & strRcptNo & " skipBrk=Yes"
			Logger.Info("ProcessStudentFeesResponse :: " & resSkipBrk)
			Return res
		End If

		'Get accounts -excluding the account that stores credit/prepayments
		strSQL = "SELECT COUNT([Names]) As TotCount FROM Accounts WHERE [StudentRelated] = 1 AND [Names] <> @acc;"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.Parameters.AddWithValue("acc", strRCreditDebitAcc)
		myReader = myCmd.ExecuteReader()
		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				lAccs = myReader.GetInt32(0)
			End If

			myReader.Close()
			Exit While
		End While

		'Get accounts -excluding the account that stores credit/prepayments
		strSQL = "SELECT [Names],Rank FROM Accounts WHERE [StudentRelated] = 1 AND [Names] <> @acc ORDER BY Rank"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.Parameters.AddWithValue("acc", strRCreditDebitAcc)
		myReader = myCmd.ExecuteReader()

		ReDim dblReqAmount(lAccs)
		ReDim dblPaidAmount(lAccs)
		ReDim dblInsertAmount(lAccs)
		ReDim booPaidAmtRead(lAccs)

		If myReader.HasRows Then
			For i = 0 To lAccs - 1
				While myReader.Read()
					If Not myReader.IsDBNull(0) Then
						listAccs.Add(myReader.GetString(0))
					End If
				End While
			Next i
		End If
		myReader.Close()

		dblInitAmount = dblAmount + dblCredit
		dblAmount += dblCredit

		For i = 0 To listAccs.Count - 1
			If dblAmount <= 0 Then Exit For
			'Get the normal invoice
			strSQL = "SELECT SUM(ISNULL([" & listAccs(i) & "],0)) AS TotItem 
                        FROM StudInvoice WHERE AdmnNo LIKE @admNo AND Rdate <= @dt;"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.Parameters.AddWithValue("admNo", admNo)
			myCmd.Parameters.AddWithValue("dt", dtDate)
			myReader = myCmd.ExecuteReader()

			While myReader.Read()
				If Not myReader.IsDBNull(0) Then
					Dim h As Decimal = myReader.GetDecimal(0)
					dblReqAmount(i) = dblReqAmount(i) + h
				End If
				myReader.Close()
				Exit While
			End While

			'Get any student invoice adjustment
			strSQL = "SELECT SUM(ISNULL([" & listAccs(i) & "],0)) AS TotItem 
                        FROM StudInvoiceAdj WHERE AdmnNo LIKE @admNo AND Rdate <= @dt;"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.Parameters.AddWithValue("admNo", admNo)
			myCmd.Parameters.AddWithValue("dt", dtDate)
			myReader = myCmd.ExecuteReader()

			While myReader.Read()
				If Not myReader.IsDBNull(0) Then
					Dim h As Decimal = myReader.GetDecimal(0)
					dblReqAmount(i) = dblReqAmount(i) + h
				End If
				myReader.Close()
				Exit While
			End While

			'Check if the paid amount for the given account has already been read
			If Not booPaidAmtRead(i) Then
				'Get receipts
				strSQL = "SELECT SUM(ISNULL([" & listAccs(i) & "],0)) AS TotItem 
                            FROM Receiptbook WHERE AdmnNo LIKE @admNo AND Rdate <= @dt 
                            AND [Receipt Number] 
                            NOT IN (SELECT [Receipt Number] 
                                          FROM ReceiptBookCanc WHERE Rdate <= @dt);"
				myCmd = myConn.CreateCommand()
				myCmd.CommandText = strSQL
				myCmd.Parameters.AddWithValue("admNo", admNo)
				myCmd.Parameters.AddWithValue("dt", dtDate)
				myReader = myCmd.ExecuteReader()
				While myReader.Read()
					If Not myReader.IsDBNull(0) Then
						Dim h As Decimal = myReader.GetDecimal(0)
						dblPaidAmount(i) = h
					End If
					myReader.Close()
					Exit While
				End While

				'Get sponsorships
				strSQL = "SELECT SUM(ISNULL([" & listAccs(i) & "],0)) AS TotItem 
                            FROM StudSponsorBD WHERE AdmnNo LIKE @admNo AND Rdate <= @dt
                            AND StudSponsorBD.[Ref] 
                            NOT IN (SELECT [Ref] 
                                    FROM StudSponsorBDCanc WHERE Rdate <= @dt);"
				myCmd = myConn.CreateCommand()
				myCmd.CommandText = strSQL
				myCmd.Parameters.AddWithValue("admNo", admNo)
				myCmd.Parameters.AddWithValue("dt", dtDate)
				myReader = myCmd.ExecuteReader()
				While myReader.Read()
					If Not myReader.IsDBNull(0) Then
						Dim h As Decimal = myReader.GetDecimal(0)
						dblPaidAmount(i) = dblPaidAmount(i) + h
					End If
					myReader.Close()
					Exit While
				End While

				'Get any refunds
				strSQL = "SELECT SUM(Amount) AS TotItem 
                            FROM Refund WHERE AdmnNo LIKE @admNo AND Account LIKE @acc 
                            AND Rdate <= @dt;"
				myCmd = myConn.CreateCommand()
				myCmd.CommandText = strSQL
				myCmd.Parameters.AddWithValue("admNo", admNo)
				myCmd.Parameters.AddWithValue("dt", dtDate)
				myCmd.Parameters.AddWithValue("acc", listAccs(i))
				myReader = myCmd.ExecuteReader()
				While myReader.Read()
					If Not myReader.IsDBNull(0) Then
						Dim h As Decimal = myReader.GetDecimal(0)
						dblPaidAmount(i) = dblPaidAmount(i) - h
					End If
					myReader.Close()
					Exit While
				End While

				strSQL = "SELECT SUM(PVDetail.[NetAmount]) AS TotItem 
                            FROM PVouchers,PVDetail WHERE PVouchers.[VoucherNo]=PVDetail.[VoucherNo] 
                            AND PVouchers.[SupRef] LIKE @admNo 
                            AND PVDetail.[Account] LIKE @acc
                            AND PVouchers.Rdate <= @dt;"
				myCmd = myConn.CreateCommand()
				myCmd.CommandText = strSQL
				myCmd.Parameters.AddWithValue("admNo", admNo)
				myCmd.Parameters.AddWithValue("dt", dtDate)
				myCmd.Parameters.AddWithValue("acc", listAccs(i))
				myReader = myCmd.ExecuteReader()

				While myReader.Read()
					If Not myReader.IsDBNull(0) Then
						Dim h As Decimal = myReader.GetDecimal(0)
						dblPaidAmount(i) = dblPaidAmount(i) - h
					End If
					myReader.Close()
					Exit While
				End While

				strSQL = "SELECT SUM(PCashDetail.[Amount]) AS TotItem 
                        FROM PCash,PCashDetail WHERE PCash.PCRef=PCashDetail.PCRef 
                        AND PCash.[PayeeRef] LIKE @admNo
                        AND PCashDetail.[Account] LIKE @acc
                        AND PCash.Rdate <= @dt;"
				myCmd = myConn.CreateCommand()
				myCmd.CommandText = strSQL
				myCmd.Parameters.AddWithValue("admNo", admNo)
				myCmd.Parameters.AddWithValue("dt", dtDate)
				myCmd.Parameters.AddWithValue("acc", listAccs(i))
				myReader = myCmd.ExecuteReader()

				While myReader.Read()
					If Not myReader.IsDBNull(0) Then
						Dim h As Decimal = myReader.GetDecimal(0)
						dblPaidAmount(i) = dblPaidAmount(i) - h
					End If
					myReader.Close()
					Exit While
				End While
				booPaidAmtRead(i) = True
			End If

			If dblPaidAmount(i) < dblReqAmount(i) And dblAmount <> 0 Then
				If dblAmount > (dblReqAmount(i) - dblPaidAmount(i)) Then
					dblInsertAmount(i) = dblReqAmount(i) - dblPaidAmount(i)
				Else
					If (dblAmount + dblInsertAmount(i)) > (dblReqAmount(i) - dblPaidAmount(i)) Then
						dblInsertAmount(i) = dblReqAmount(i) - dblPaidAmount(i)
					Else
						dblInsertAmount(i) = dblInsertAmount(i) + dblAmount
					End If
				End If

				dblSpentAmount = 0

				For y = 0 To listAccs.Count - 1
					dblSpentAmount += dblInsertAmount(y)
				Next y

				dblAmount = dblInitAmount - dblSpentAmount
			End If
		Next i

		Dim strTempSql As String = "UPDATE Receiptbook SET "
		For i = 0 To listAccs.Count - 1
			If dblInsertAmount(i) > 0 Then
				strTempSql += " [" & listAccs(i) & "] = " & dblInsertAmount(i) & ","
			Else
				strTempSql += " [" & listAccs(i) & "] = NULL,"
			End If
		Next i

		strTempSql = strTempSql.Trim().Remove(strTempSql.Length - 1)
		strTempSql += "  WHERE [Receipt Number] LIKE '" & strRcptNo & "';"
		strSQL = strTempSql
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.CommandType = CommandType.Text

		Dim updatedRow As Integer = myCmd.ExecuteNonQuery()
		Console.WriteLine("RowsAffected: {0}", updatedRow)

		If dblAmount - dblCredit > 0 Then
			'Save the excess amount
			strSQL = "INSERT INTO RCredit ([Receipt Number],AdmnNo,Curr,Account,Amount)
                        VALUES(@rcpt,@admNo,@curr,@acc,@amt);"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.CommandType = CommandType.Text
			myCmd.Parameters.AddWithValue("rcpt", strRcptNo)
			myCmd.Parameters.AddWithValue("admNo", admNo)
			myCmd.Parameters.AddWithValue("curr", strCur)
			myCmd.Parameters.AddWithValue("acc", strRCreditDebitAcc)
			myCmd.Parameters.AddWithValue("amt", dblAmount - dblCredit)

			Dim affectedRow As Integer = myCmd.ExecuteNonQuery()
			Console.WriteLine("RowsAffected: {0}", affectedRow)
		End If

		'Save credit used only if spent
		If dblCredit > 0 And dblCredit = dblSpentAmount Then
			strSQL = "INSERT INTO RDebit ([Receipt Number],AdmnNo,Curr,Account,Amount) 
                    VALUES(@rcpt,@admNo,@curr,@acc,@amt);"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.CommandType = CommandType.Text
			myCmd.Parameters.AddWithValue("rcpt", strRcptNo)
			myCmd.Parameters.AddWithValue("admNo", admNo)
			myCmd.Parameters.AddWithValue("curr", strCur)
			myCmd.Parameters.AddWithValue("acc", strRCreditDebitAcc)
			myCmd.Parameters.AddWithValue("amt", dblCredit)

			Dim affectedRow As Integer = myCmd.ExecuteNonQuery()
			Console.WriteLine("RowsAffected: {0}", affectedRow)

		ElseIf dblCredit > 0 Then
			'Delete this credit receipt
			strSQL = "DELETE FROM receiptbook WHERE [Receipt Number] LIKE @rcpt;"
			myCmd = myConn.CreateCommand()
			myCmd.CommandText = strSQL
			myCmd.CommandType = CommandType.Text
			myCmd.Parameters.AddWithValue("rcpt", strRcptNo)

			Dim rows As Integer = myCmd.ExecuteNonQuery()
			Console.WriteLine("RowsAffected: {0}", rows)
		End If

		'strSQL = "DELETE FROM receiptbreak WHERE [Receipt Number] LIKE @rcpt;"
		'myCmd = myConn.CreateCommand()
		'myCmd.CommandText = strSQL
		'myCmd.CommandType = CommandType.Text
		'myCmd.Parameters.AddWithValue("rcpt", strRcptNo)

		'Dim affectedRows As Integer = myCmd.ExecuteNonQuery()
		'Console.WriteLine("RowsAffected: {0}", affectedRows)
		myConn.Close()
		res.strStatus = "OK"
		res.strRcptNo = strRcptNo
		res.strMsg = "SUCCESS"

		Dim resBrk = " strRegNo=" & strRegNo & ", strUN=" & strUN & " ,strStatus=" & res.strStatus & " ,strMsg=" & res.strMsg & " ,strType=" & strType & " ,strRcptNo=" & strRcptNo & " skipBrk=No"
		Logger.Info("ProcessStudentFeesResponse :: " & resBrk)
		Return res
	End Function

	Private Function ProcessApplicantFees(ByVal strRcptNo As String, ByVal strCampus As String, ByVal strProgName As String, ByVal dblAmount As Double) As String
		Dim strDept = ""
		Dim strAcc = ""
		strSQL = "SELECT [department] FROM Programme WHERE [names]= @prog "
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.Parameters.AddWithValue("prog", strProgName)
		myReader = myCmd.ExecuteReader()
		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				strDept = myReader.GetString(0)
			End If
			myReader.Close()
			Exit While
		End While

		'strSQL = "SELECT TOP (1)  [Names]  FROM  Accounts
		'		  WHERE Names LIKE '%Application%' AND StudentRelated=0"
		strSQL = "SELECT TOP (1)  [Names]  FROM  Accounts
				  WHERE Names LIKE '%Application%'"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myReader = myCmd.ExecuteReader()
		While myReader.Read()
			If Not myReader.IsDBNull(0) Then
				strAcc = myReader.GetString(0)
			End If
			myReader.Close()
			Exit While
		End While

		strSQL = "INSERT INTO ReceiptBookOtherDetail ([Receipt Number],[Campus],[Department],[Account], [Amount],[Notes])
                    VALUES(@rcptNo,@campus,@dept,@acc, @amt,@notes);"

		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.CommandType = CommandType.Text
		myCmd.Parameters.AddWithValue("rcptNo", strRcptNo)
		myCmd.Parameters.AddWithValue("campus", strCampus)
		myCmd.Parameters.AddWithValue("dept", strDept)
		myCmd.Parameters.AddWithValue("acc", strAcc)
		myCmd.Parameters.AddWithValue("amt", dblAmount)
		myCmd.Parameters.AddWithValue("notes", "Auto Receipted")

		Dim rowsAffected As Integer = myCmd.ExecuteNonQuery()
		Console.WriteLine("RowsAffected: {0}", rowsAffected)

		strSQL = "UPDATE [ReceiptBook] SET AdmnNo=NULL  WHERE [Receipt Number]= @rcptNo"
		myCmd = myConn.CreateCommand()
		myCmd.CommandText = strSQL
		myCmd.CommandType = CommandType.Text
		myCmd.Parameters.AddWithValue("rcptNo", strRcptNo)

		Dim rowsAffectedUpdate As Integer = myCmd.ExecuteNonQuery()
		Console.WriteLine("RowsAffectedUpdate: {0}", rowsAffectedUpdate)

		Dim appRes = " Update strRcptNo=" & strRcptNo & ", strAccount= " & strAcc & ", strCampus=" & strCampus & ""
		Logger.Info("ProcessApplicantFeesUpdate :: " & appRes)
		Return "SUCCESS"
	End Function


End Class

<WebService(Namespace:="unisol:bankapi:response")>
Public Class FeeProcessResponse
	Public strRegNo As String
	Public strTransNo As String
	Public strUN As String
	Public strRcptNo As String
	Public strStatus As String
	Public strMsg As String
End Class

<WebService(Namespace:="unisol:bankapi:response")>
Public Class CheckStudentResponse
	Public strRegNo As String
	Public strStudName As String
	Public strUN As String
	Public strStatus As String
	Public strMsg As String
End Class