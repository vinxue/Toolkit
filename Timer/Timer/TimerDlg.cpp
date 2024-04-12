// TimerDlg.cpp : implementation file
//

/*********************************************************
Author: Gavin
Date: 12/10/2009
Version: 1.0.1.0
History: First release
Known Issue: If selected time is invalid, the tool is unable to detect.
***********************************************************/

/*********************************************************
Author: Gavin
Date: 12/18/2009
Version: 1.0.1.1
History: 1.Fixed the issue of can't reboot and shutdown(Authority is insufficient).
Known Issue: If selected time is invalid, the tool is unable to detect.
***********************************************************/

/*********************************************************
Author: Gavin
Date: 12/21/2009
Version: 1.0.2.0
History: 1.Add the function of countdown.
Known Issue: If selected time is invalid, the tool is unable to detect.
***********************************************************/

/*********************************************************
Author: Gavin
Date: 12/22/2009
Version: 1.0.2.1
History: 1.Add the function of display countdown dynamically. 
Known Issue: If selected time is invalid, the tool is unable to detect.
***********************************************************/

/*********************************************************
Author: Gavin
Date: 12/25/2009
Version: 1.0.2.2
History:
1.Fix the issue of can't detect invalid time.
2.Add a contact in "About" dialog.
Known Issue: None
***********************************************************/

/*********************************************************
Author: Gavin
Date: 12/26/2009
Version: 1.0.2.3
History: 1.Fixed SetTimer() start point issue.
Known Issue: None
***********************************************************/

/*********************************************************
Author: Gavin
Date: 01/21/2010
Version: 1.0.2.4
History: 1.Fixed action option 2 can't activated issue.
Known Issue: None
***********************************************************/

/*********************************************************
Author: Gavin
Date: 04/01/2024
Version: 1.0.2.5
History: 1.Removed unavailable contact information.
2. Update build toolchain to VS2019 from VS2008.
Known Issue: None
***********************************************************/

/*********************************************************
Author: Gavin
Date: 04/12/2024
Version: 1.0.2.6
History: 1.Prevent the display and sleep idle timeout
Known Issue: None
***********************************************************/

#include "stdafx.h"
#include "Timer.h"
#include "TimerDlg.h"
#include "PowrProf.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif


int m_Year, m_Month, m_Today, m_Hour, m_Minute, m_Second;
// CAboutDlg dialog used for App About

class CAboutDlg : public CDialog
{
public:
	CAboutDlg();

// Dialog Data
	enum { IDD = IDD_ABOUTBOX };

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);    // DDX/DDV support

// Implementation
protected:
	DECLARE_MESSAGE_MAP()
public:
	afx_msg void OnNMClickSyslinkMail(NMHDR *pNMHDR, LRESULT *pResult);
	afx_msg void OnNMClickSyslinkSite(NMHDR *pNMHDR, LRESULT *pResult);
};

CAboutDlg::CAboutDlg() : CDialog(CAboutDlg::IDD)
{
}

void CAboutDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CAboutDlg, CDialog)
END_MESSAGE_MAP()


// CTimerDlg dialog




CTimerDlg::CTimerDlg(CWnd* pParent /*=NULL*/)
	: CDialog(CTimerDlg::IDD, pParent)
	, m_strFile(_T(""))
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CTimerDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_DATETIMEPICKER_DATE, m_Date);
	DDX_Control(pDX, IDC_DATETIMEPICKER_TIME, m_Time);
	DDX_Text(pDX, IDC_EDIT1, m_strFile);
	DDX_Control(pDX, IDC_BUTTON_ACTIVATE, m_cActivate);
	DDX_Control(pDX, IDC_BUTTON_PAUSE, m_cPause);
	DDX_Control(pDX, IDC_DATETIMEPICKER1, m_Count);
}

BEGIN_MESSAGE_MAP(CTimerDlg, CDialog)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	//}}AFX_MSG_MAP
	ON_WM_TIMER()
	ON_BN_CLICKED(IDC_BUTTON_ACTIVATE, &CTimerDlg::OnBnClickedButtonActivate)
	ON_BN_CLICKED(IDC_BUTTON_BROWSE, &CTimerDlg::OnBnClickedButtonBrowse)
	ON_BN_CLICKED(IDC_BUTTON_PAUSE, &CTimerDlg::OnBnClickedButtonPause)
	ON_BN_CLICKED(IDC_BUTTON_ABOUT, &CTimerDlg::OnBnClickedButtonAbout)
END_MESSAGE_MAP()


// CTimerDlg message handlers

BOOL CTimerDlg::OnInitDialog()
{
	CDialog::OnInitDialog();

	// Add "About..." menu item to system menu.

	// IDM_ABOUTBOX must be in the system command range.
	ASSERT((IDM_ABOUTBOX & 0xFFF0) == IDM_ABOUTBOX);
	ASSERT(IDM_ABOUTBOX < 0xF000);

	CMenu* pSysMenu = GetSystemMenu(FALSE);
	if (pSysMenu != NULL)
	{
		CString strAboutMenu;
		strAboutMenu.LoadString(IDS_ABOUTBOX);
		if (!strAboutMenu.IsEmpty())
		{
			pSysMenu->AppendMenu(MF_SEPARATOR);
			pSysMenu->AppendMenu(MF_STRING, IDM_ABOUTBOX, strAboutMenu);
		}
	}

	// Set the icon for this dialog.  The framework does this automatically
	//  when the application's main window is not a dialog
	SetIcon(m_hIcon, TRUE);			// Set big icon
	SetIcon(m_hIcon, FALSE);		// Set small icon

	// TODO: Add extra initialization here
	//SetTimer(0, 250, NULL);
	CTime tm(0, 0, 0);
	m_Count.SetTime(&tm);
	m_Count.SetFormat(L"HH:mm:ss");


	static HANDLE hToken;
	static TOKEN_PRIVILEGES tp;
	static LUID luid;

	if (::OpenProcessToken( GetCurrentProcess(),
		TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &hToken))
	{
		::LookupPrivilegeValue( NULL, SE_SHUTDOWN_NAME, &luid );
		tp.PrivilegeCount = 1;
		tp.Privileges[0].Luid = luid;
		tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
		::AdjustTokenPrivileges( hToken, FALSE, &tp,
			sizeof( TOKEN_PRIVILEGES), NULL, NULL );
	}

	m_cActivate.ShowWindow(TRUE);
	m_cPause.ShowWindow(FALSE);

	CButton *pRadion;
	pRadion = (CButton *)GetDlgItem(IDC_RADIO_EXECUTE);
	pRadion->SetCheck(TRUE);

	((CButton *)GetDlgItem(IDC_RADIO_TIME))->SetCheck(TRUE);



	CComboBox* ComboCtrl = (CComboBox*)GetDlgItem(IDC_COMBO_ACTION);
	ComboCtrl->AddString(L"Log Off");
	ComboCtrl->AddString(L"Restart");
	ComboCtrl->AddString(L"Suspend");
	ComboCtrl->AddString(L"Hibernate");
	ComboCtrl->AddString(L"Shut Down");

	//ComboCtrl->SetCurSel(0);


	return TRUE;  // return TRUE  unless you set the focus to a control
}

void CTimerDlg::OnSysCommand(UINT nID, LPARAM lParam)
{
	if ((nID & 0xFFF0) == IDM_ABOUTBOX)
	{
		CAboutDlg dlgAbout;
		dlgAbout.DoModal();
	}
	else
	{
		CDialog::OnSysCommand(nID, lParam);
	}
}

// If you add a minimize button to your dialog, you will need the code below
//  to draw the icon.  For MFC applications using the document/view model,
//  this is automatically done for you by the framework.

void CTimerDlg::OnPaint()
{
	if (IsIconic())
	{
		CPaintDC dc(this); // device context for painting

		SendMessage(WM_ICONERASEBKGND, reinterpret_cast<WPARAM>(dc.GetSafeHdc()), 0);

		// Center icon in client rectangle
		int cxIcon = GetSystemMetrics(SM_CXICON);
		int cyIcon = GetSystemMetrics(SM_CYICON);
		CRect rect;
		GetClientRect(&rect);
		int x = (rect.Width() - cxIcon + 1) / 2;
		int y = (rect.Height() - cyIcon + 1) / 2;

		// Draw the icon
		dc.DrawIcon(x, y, m_hIcon);
	}
	else
	{
		CDialog::OnPaint();
	}
}

// The system calls this function to obtain the cursor to display while the user drags
//  the minimized window.
HCURSOR CTimerDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}


void CTimerDlg::OnTimer(UINT_PTR nIDEvent)
{
	// TODO: Add your message handler code here and/or call default
	if (nIDEvent == 0)
	{
		BOOL DateFlag;

		if (TRUE == ((CButton *)GetDlgItem(IDC_RADIO_TIME))->GetCheck())
		{
			SYSTEMTIME systime;
			GetLocalTime(&systime);

			DateFlag = (m_Year == systime.wYear) && (m_Month == systime.wMonth)
				&& (m_Today == systime.wDay) && (m_Hour == systime.wHour)
				&& (m_Minute == systime.wMinute) && (m_Second == systime.wSecond);
		}

		else if (TRUE == ((CButton *)GetDlgItem(IDC_RADIO_TIMER))->GetCheck())
		{
			int DisHour, DisMin, DisSec;

			if (Count > GetTickCount() )
			{
				DisHour = (Count - GetTickCount())/3600000;
				DisMin = ((Count - GetTickCount())%3600000)/60000;
				DisSec = ((Count - GetTickCount())%3600000)%60000/1000;

				CTime SysTm = CTime::GetCurrentTime();				

				CTime tm(SysTm.GetYear(), SysTm.GetMonth(), SysTm.GetDay(), DisHour, DisMin, DisSec);
				m_Count.SetTime(&tm);				
			}

			DateFlag = (GetTickCount()/1000 == TargetCount);
		}

		CButton *pRadion1, *pRadion2;
		pRadion1 = (CButton *)GetDlgItem(IDC_RADIO_ACTION);
		pRadion2 = (CButton *)GetDlgItem(IDC_RADIO_EXECUTE);

		if (DateFlag == TRUE)
		{
			m_cActivate.ShowWindow(TRUE);
			m_cPause.ShowWindow(FALSE);

			m_Date.EnableWindow(TRUE);
			m_Time.EnableWindow(TRUE);

			// Clear EXECUTION_STATE flags to allow the display to idle and allow the system to idle to sleep normally
			SetThreadExecutionState(ES_CONTINUOUS);  // Gavin 04/12/2024

			if (TRUE == pRadion1->GetCheck())
			{
				CComboBox* comboctrl = (CComboBox*)GetDlgItem(IDC_COMBO_ACTION);
				int index = comboctrl->GetCurSel();

				if (index == 0)
				{
					KillTimer(0);					
					if (!ExitWindowsEx(EWX_LOGOFF, 0))
						AfxMessageBox(L"Log off is failed!", MB_ICONINFORMATION);
				}
				else if (index == 1)
				{
					KillTimer(0);
					if (!ExitWindowsEx(EWX_REBOOT, 0))
						AfxMessageBox(L"Restart is failed!", MB_ICONINFORMATION);
				}
				else if (index == 2)
				{
					KillTimer(0);
					if (!SetSuspendState(FALSE, FALSE, FALSE))
						AfxMessageBox(L"Suspend is failed!", MB_ICONINFORMATION);

				}
				else if (index == 3)
				{
					KillTimer(0);
					if (!SetSuspendState(TRUE, FALSE, FALSE))
						AfxMessageBox(L"Hibernate is failed!", MB_ICONINFORMATION);
				}
				else if (index == 4)
				{
					KillTimer(0);
					if (!ExitWindowsEx(EWX_SHUTDOWN|EWX_POWEROFF, 0))
						AfxMessageBox(L"Shutdown is failed!", MB_ICONINFORMATION);
				}
			}

			else if (TRUE == pRadion2->GetCheck())
			{
				KillTimer(0);
				if (m_strFile != "")
				{
					ShellExecute(m_hWnd, L"open", m_strFile, NULL, NULL, SW_SHOW);
				}
			}
		}
	}

	CDialog::OnTimer(nIDEvent);
}

void CTimerDlg::OnBnClickedButtonActivate()
{
	// TODO: Add your control notification handler code here

	BOOL Flag = FALSE;

	if (TRUE == ((CButton *)GetDlgItem(IDC_RADIO_TIME))->GetCheck())
	{
		CTime TargetDate, TargetTime;
		m_Date.GetTime(TargetDate);
		m_Time.GetTime(TargetTime);

		m_Year = TargetDate.GetYear();
		m_Month = TargetDate.GetMonth();
		m_Today = TargetDate.GetDay();
		m_Hour = TargetTime.GetHour();
		m_Minute = TargetTime.GetMinute();
		m_Second = TargetTime.GetSecond();

		CTime tm = CTime::GetCurrentTime();            //Fix the issue of can't detect invalid time.    12/25/2009  Gavin
		CTime TarTm(m_Year, m_Month, m_Today, m_Hour, m_Minute, m_Second);

		if (TarTm < tm)
		{
			Flag = FALSE;
			AfxMessageBox(L"Selected time is invalid, Please try again.", MB_ICONINFORMATION);			
		}
		else
		{
			Flag = TRUE;

			m_Date.EnableWindow(FALSE);
			m_Time.EnableWindow(FALSE);
		}
	}

	else if (TRUE == ((CButton *)GetDlgItem(IDC_RADIO_TIMER))->GetCheck())
	{
		CTime CountTime;
		m_Count.GetTime(CountTime);

		int mHour, mMin, mSec;

		mHour = CountTime.GetHour();
		mMin = CountTime.GetMinute();
		mSec = CountTime.GetSecond();

		Count = GetTickCount() + mHour*3600*1000 + mMin*60*1000 + mSec*1000;

		TargetCount  = Count/1000;

		Flag = TRUE;
	}

	if (Flag == TRUE)
	{
		int index = ((CComboBox *)GetDlgItem(IDC_COMBO_ACTION))->GetCurSel();

		if ((index != CB_ERR) && (m_strFile == "")) 
		{
			((CButton *)GetDlgItem(IDC_RADIO_ACTION))->SetCheck(TRUE);
			((CButton *)GetDlgItem(IDC_RADIO_EXECUTE))->SetCheck(FALSE);

			m_cActivate.ShowWindow(FALSE);
			m_cPause.ShowWindow(TRUE);

			SetTimer(0, 200, NULL);                           //Fixed action option 2 can't activated issue     01/21/2010  Gavin
			// Prevent the display and sleep idle time-out
			SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);  // Gavin 04/12/2024
		}

		else if ((m_strFile == "") && (index == CB_ERR))
		{
			m_Date.EnableWindow(TRUE);
			m_Time.EnableWindow(TRUE);

			AfxMessageBox(L"Please select a action option.", MB_ICONINFORMATION);		
		}
		else
		{
			m_cActivate.ShowWindow(FALSE);
			m_cPause.ShowWindow(TRUE);	

			SetTimer(0, 200, NULL);                         //Modify by Gavin     12/26/2009
			// Prevent the display and sleep idle time-out
			SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);  // Gavin 04/12/2024
		}

		//SetTimer(0, 200, NULL);
	}
}

void CTimerDlg::OnBnClickedButtonBrowse()
{
	((CButton *)GetDlgItem(IDC_RADIO_EXECUTE))->SetCheck(TRUE);
	((CButton *)GetDlgItem(IDC_RADIO_ACTION))->SetCheck(FALSE);

	WCHAR szFilters[] = L"All Files (*.*)|*.*||";
	CFileDialog fileDlg(TRUE, L"All Files", L"*.*", OFN_FILEMUSTEXIST, szFilters, this);
	if (fileDlg.DoModal() == IDOK)
	{
		m_strFile = fileDlg.GetPathName();
		UpdateData(FALSE);
	}
}

void CTimerDlg::OnBnClickedButtonPause()
{
	// TODO: Add your control notification handler code here
	KillTimer(0);
	m_cActivate.ShowWindow(TRUE);
	m_cPause.ShowWindow(FALSE);

	m_Date.EnableWindow(TRUE);
	m_Time.EnableWindow(TRUE);
}

void CTimerDlg::OnBnClickedButtonAbout()
{
	// TODO: Add your control notification handler code here
	CAboutDlg dlgAbout;
	dlgAbout.DoModal();
}
