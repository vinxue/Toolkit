
// KbSimDlg.cpp : implementation file
//

#include "stdafx.h"
#include "KbSim.h"
#include "KbSimDlg.h"
#include "afxdialogex.h"
#include <PowrProf.h>

#pragma comment (lib, "PowrProf.lib")

#ifdef _DEBUG
#define new DEBUG_NEW
#endif

enum SystemState
{
	STATE_DO_NOTHING,
	STATE_LOG_OFF,
	STATE_RESTART,
	STATE_SUSPEND,
	STATE_HIBERNATE,
	STATE_SHUTDOWN,
	STATE_MAX
};

UINT8 mSysState[STATE_MAX] = { 0 };
DWORD TargetCount;
DWORD Count;
BOOLEAN mPrivilegeFlag = FALSE;
BOOLEAN mActivityFlag = FALSE;

#define ID_EVENT_KB 0
#define ID_EVENT_COUNTDOWN 1

// CAboutDlg dialog used for App About

class CAboutDlg : public CDialogEx
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
};

CAboutDlg::CAboutDlg() : CDialogEx(CAboutDlg::IDD)
{
}

void CAboutDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CAboutDlg, CDialogEx)
END_MESSAGE_MAP()


// CKbSimDlg dialog



CKbSimDlg::CKbSimDlg(CWnd* pParent /*=NULL*/)
	: CDialogEx(CKbSimDlg::IDD, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CKbSimDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CKbSimDlg, CDialogEx)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	ON_WM_TIMER()
	ON_BN_CLICKED(IDC_BUTTON_RUN, &CKbSimDlg::OnBnClickedButtonRun)
	ON_BN_CLICKED(IDC_BUTTON_STOP, &CKbSimDlg::OnBnClickedButtonStop)
	ON_WM_CTLCOLOR()
	ON_WM_CLOSE()
END_MESSAGE_MAP()


// CKbSimDlg message handlers

BOOL CKbSimDlg::OnInitDialog()
{
	CDialogEx::OnInitDialog();

	// Add "About..." menu item to system menu.

	// IDM_ABOUTBOX must be in the system command range.
	ASSERT((IDM_ABOUTBOX & 0xFFF0) == IDM_ABOUTBOX);
	ASSERT(IDM_ABOUTBOX < 0xF000);

	CMenu* pSysMenu = GetSystemMenu(FALSE);
	if (pSysMenu != NULL)
	{
		BOOL bNameValid;
		CString strAboutMenu;
		bNameValid = strAboutMenu.LoadString(IDS_ABOUTBOX);
		ASSERT(bNameValid);
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
	CString         Str;
	UINT8           Index;
	UINT8           StateIndex;
	BOOLEAN         PwrCapResult;
	SYSTEM_POWER_CAPABILITIES PowerCaps;

	((CButton*)GetDlgItem(IDC_BUTTON_STOP))->ShowWindow(FALSE);

	CFont* font;
	font = new CFont;
	font->CreateFont(16, 0, 0, 0, FW_BOLD/*FW_MEDIUM*//*FW_NORMAL*/, FALSE, FALSE, 0, DEFAULT_CHARSET,
		OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH, _T("Arial"));
	GetDlgItem(IDC_STATIC_KbSim)->SetFont(font);

	// Set up interval time
	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->SetLimitText(3);
	Str.Format(L"%d", 60);
	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->SetWindowText(Str);

	// Set up countdown
	CTime tm(0, 0, 0);
	((CDateTimeCtrl*)GetDlgItem(IDC_DATETIMEPICKER_COUNTDOWN))->SetTime(&tm);
	((CDateTimeCtrl*)GetDlgItem(IDC_DATETIMEPICKER_COUNTDOWN))->SetFormat(L"HH:mm:ss");

	// Set system state after stop
	PwrCapResult = TRUE;
	if (!GetPwrCapabilities(&PowerCaps)) {
		PwrCapResult = FALSE;
	}

	StateIndex = 0;
	for (Index = 0; Index < STATE_MAX; Index++)
	{
		switch (Index)
		{
		case STATE_DO_NOTHING:
			((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->AddString(L"Do nothing");
			mSysState[StateIndex] = STATE_DO_NOTHING;
			StateIndex++;
			break;
		case STATE_LOG_OFF:
			((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->AddString(L"Log off");
			mSysState[StateIndex] = STATE_LOG_OFF;
			StateIndex++;
			break;
		case STATE_RESTART:
			((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->AddString(L"Restart");
			mSysState[StateIndex] = STATE_RESTART;
			StateIndex++;
			break;
		case STATE_SUSPEND:
			if (PwrCapResult)
			{
				if (PowerCaps.SystemS3)
				{
					((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->AddString(L"Suspend");
					mSysState[StateIndex] = STATE_SUSPEND;
					StateIndex++;
				}
			}
			break;
		case STATE_HIBERNATE:
			if (PwrCapResult)
			{
				if (PowerCaps.SystemS4 && PowerCaps.HiberFilePresent)
				{
					((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->AddString(L"Hibernate");
					mSysState[StateIndex] = STATE_HIBERNATE;
					StateIndex++;
				}
			}
			break;
		case STATE_SHUTDOWN:
			((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->AddString(L"Shut down");
			mSysState[StateIndex] = STATE_SHUTDOWN;
			StateIndex++;
			break;
		default:
			break;
		}
	}
	((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->SetCurSel(STATE_DO_NOTHING);

	return TRUE;  // return TRUE  unless you set the focus to a control
}

void CKbSimDlg::OnSysCommand(UINT nID, LPARAM lParam)
{
	if ((nID & 0xFFF0) == IDM_ABOUTBOX)
	{
		CAboutDlg dlgAbout;
		dlgAbout.DoModal();
	}
	else
	{
		CDialogEx::OnSysCommand(nID, lParam);
	}
}

// If you add a minimize button to your dialog, you will need the code below
//  to draw the icon.  For MFC applications using the document/view model,
//  this is automatically done for you by the framework.

void CKbSimDlg::OnPaint()
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
		CDialogEx::OnPaint();
	}
}

// The system calls this function to obtain the cursor to display while the user drags
//  the minimized window.
HCURSOR CKbSimDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}


BOOL SetPrivilege(
	LPCTSTR lpszPrivilege,  // name of privilege to enable/disable
	BOOL bEnablePrivilege   // to enable or disable privilege
)
{
	HANDLE hToken;  // access token handle
	TOKEN_PRIVILEGES tp;
	LUID luid;

	if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &hToken))
	{
		return FALSE;
	}

	if (!LookupPrivilegeValue(
		NULL,            // lookup privilege on local system
		lpszPrivilege,   // privilege to lookup 
		&luid))        // receives LUID of privilege
	{
		// printf("LookupPrivilegeValue error: %u\n", GetLastError());
		return FALSE;
	}

	tp.PrivilegeCount = 1;
	tp.Privileges[0].Luid = luid;
	if (bEnablePrivilege)
		tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
	else
		tp.Privileges[0].Attributes = 0;

	// Enable the privilege or disable all privileges.

	if (!AdjustTokenPrivileges(
		hToken,
		FALSE,
		&tp,
		sizeof(TOKEN_PRIVILEGES),
		(PTOKEN_PRIVILEGES)NULL,
		(PDWORD)NULL))
	{
		// printf("AdjustTokenPrivileges error: %u\n", GetLastError());
		return FALSE;
	}

	if (GetLastError() == ERROR_NOT_ALL_ASSIGNED)

	{
		// printf("The token does not have the specified privilege. \n");
		return FALSE;
	}

	return TRUE;
}


void CKbSimDlg::OnTimer(UINT_PTR nIDEvent)
{
	// TODO: Add your message handler code here and/or call default
	switch (nIDEvent)
	{
	case ID_EVENT_KB:
		keybd_event(VK_CAPITAL, 0, 0, 0);
		keybd_event(VK_CAPITAL, 0, KEYEVENTF_KEYUP, 0);
		break;
	case ID_EVENT_COUNTDOWN:
		if (((CButton*)GetDlgItem(IDC_CHECK_COUNTDOWN))->GetCheck() == TRUE)
		{
			int DisHour, DisMin, DisSec;

			if (Count > GetTickCount())
			{
				DisHour = (Count - GetTickCount()) / 3600000;
				DisMin = ((Count - GetTickCount()) % 3600000) / 60000;
				DisSec = ((Count - GetTickCount()) % 3600000) % 60000 / 1000;

				CTime SysTm = CTime::GetCurrentTime();

				CTime tm(SysTm.GetYear(), SysTm.GetMonth(), SysTm.GetDay(), DisHour, DisMin, DisSec);
				((CDateTimeCtrl*)GetDlgItem(IDC_DATETIMEPICKER_COUNTDOWN))->SetTime(&tm);
			}

			if (GetTickCount() / 1000 == TargetCount)
			{
				OnBnClickedButtonStop();

				int SelIdx = ((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->GetCurSel();
				if (SelIdx != CB_ERR)
				{
					switch (mSysState[SelIdx])
					{
					case STATE_LOG_OFF:
						if (!ExitWindowsEx(EWX_LOGOFF, 0))
							AfxMessageBox(L"Log off is failed!", MB_ICONINFORMATION);
						break;
					case STATE_RESTART:
						if (!ExitWindowsEx(EWX_REBOOT, 0))
							AfxMessageBox(L"Restart is failed!", MB_ICONINFORMATION);
						break;
					case STATE_SUSPEND:
						if (!SetSuspendState(FALSE, FALSE, FALSE))
							AfxMessageBox(L"Suspend is failed!", MB_ICONINFORMATION);
						break;
					case STATE_HIBERNATE:
						if (!SetSuspendState(TRUE, FALSE, FALSE))
							AfxMessageBox(L"Hibernate is failed!", MB_ICONINFORMATION);
						break;
					case STATE_SHUTDOWN:
						if (!ExitWindowsEx(EWX_SHUTDOWN | EWX_POWEROFF, 0))
							AfxMessageBox(L"Shutdown is failed!", MB_ICONINFORMATION);
						break;
					default:
						break;
					}
				}
			}
		}
		break;
	default:
		break;
	}

	CDialogEx::OnTimer(nIDEvent);
}



void CKbSimDlg::OnBnClickedButtonRun()
{
	// TODO: Add your control notification handler code here
	CString StrIntervalTime;
	UINT32 IntervalTime;
	SYSTEM_POWER_POLICY SystemPowerPolicy;

	// Step 1: Get interval time
	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->GetWindowText(StrIntervalTime);
	IntervalTime = wcstoul(StrIntervalTime, NULL, 10);

	if ((IntervalTime < 1) || (IntervalTime > 299))
	{
		AfxMessageBox(L"Please input a valid value (1 ~ 299).\n");
		return;
	}

	// Step 2: Get countdown setting
	if (((CButton*)GetDlgItem(IDC_CHECK_COUNTDOWN))->GetCheck() == TRUE)
	{
		CTime CountTime;
		((CDateTimeCtrl*)GetDlgItem(IDC_DATETIMEPICKER_COUNTDOWN))->GetTime(CountTime);

		int mHour, mMin, mSec;

		mHour = CountTime.GetHour();
		mMin = CountTime.GetMinute();
		mSec = CountTime.GetSecond();
		if ((mHour == 0) && (mMin == 0) && (mSec == 0))
		{
			AfxMessageBox(L"Please set a value of countdown.");
			return;
		}

		Count = GetTickCount() + mHour * 3600 * 1000 + mMin * 60 * 1000 + mSec * 1000;
		TargetCount = Count / 1000;

		// Setp 3: Check system state after countdown ends
		int SelIdx = ((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->GetCurSel();
		if (SelIdx != CB_ERR)
		{
			if ((!mPrivilegeFlag) && (mSysState[SelIdx] != STATE_DO_NOTHING))
			{
				if (!SetPrivilege(SE_SHUTDOWN_NAME, TRUE))
				{
					AfxMessageBox(L"Set privilege failed.");
					return;
				}
				else
				{
					mPrivilegeFlag = TRUE;
				}
			}
		}
	}

	// Step 4: Prevent the display and sleep idle time-out
	if (!CallNtPowerInformation(SystemPowerPolicyCurrent, NULL, 0, &SystemPowerPolicy, sizeof(SYSTEM_POWER_POLICY)))
	{
		if ((IntervalTime >= SystemPowerPolicy.VideoTimeout) && !mActivityFlag)
		{
			SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
			mActivityFlag = TRUE;
		}
	}

	// Step 5: Start timer
	if (((CButton*)GetDlgItem(IDC_CHECK_COUNTDOWN))->GetCheck() == TRUE)
	{
		((CButton*)GetDlgItem(IDC_CHECK_COUNTDOWN))->EnableWindow(FALSE);
		((CDateTimeCtrl*)GetDlgItem(IDC_DATETIMEPICKER_COUNTDOWN))->EnableWindow(FALSE);
		((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->EnableWindow(FALSE);
		SetTimer(ID_EVENT_COUNTDOWN, 500, NULL);
	}

	SetTimer(ID_EVENT_KB, IntervalTime * 1000, NULL);

	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->EnableWindow(FALSE);

	((CButton*)GetDlgItem(IDC_BUTTON_RUN))->ShowWindow(FALSE);
	((CButton*)GetDlgItem(IDC_BUTTON_STOP))->ShowWindow(TRUE);
}


void CKbSimDlg::OnBnClickedButtonStop()
{
	// TODO: Add your control notification handler code here
	KillTimer(ID_EVENT_KB);
	if (((CButton*)GetDlgItem(IDC_CHECK_COUNTDOWN))->GetCheck() == TRUE)
	{
		((CButton*)GetDlgItem(IDC_CHECK_COUNTDOWN))->EnableWindow(TRUE);
		((CDateTimeCtrl*)GetDlgItem(IDC_DATETIMEPICKER_COUNTDOWN))->EnableWindow(TRUE);
		((CComboBox*)GetDlgItem(IDC_COMBO_ACTION))->EnableWindow(TRUE);
		KillTimer(ID_EVENT_COUNTDOWN);
	}

	// Clear EXECUTION_STATE flags to allow the display to idle and allow the system to idle to sleep normally.
	if (mActivityFlag)
	{
		SetThreadExecutionState(ES_CONTINUOUS);
		mActivityFlag = FALSE;
	}

	if (LOBYTE(GetKeyState(VK_CAPITAL)))
	{
		keybd_event(VK_CAPITAL, 0, 0, 0);
		keybd_event(VK_CAPITAL, 0, KEYEVENTF_KEYUP, 0);
	}

	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->EnableWindow(TRUE);

	((CButton*)GetDlgItem(IDC_BUTTON_RUN))->ShowWindow(TRUE);
	((CButton*)GetDlgItem(IDC_BUTTON_STOP))->ShowWindow(FALSE);
}


HBRUSH CKbSimDlg::OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor)
{
	HBRUSH hbr = CDialogEx::OnCtlColor(pDC, pWnd, nCtlColor);

	// TODO:  Change any attributes of the DC here

	// TODO:  Return a different brush if the default is not desired
	if (pWnd->GetDlgCtrlID() == IDC_STATIC_KbSim)
	{
		pDC->SetTextColor(RGB(0, 102, 129));
		pDC->SetBkMode(TRANSPARENT);
		return hbr;
	}
	return hbr;
}


void CKbSimDlg::OnClose()
{
	// TODO: Add your message handler code here and/or call default
	OnBnClickedButtonStop();

	CDialogEx::OnClose();
}


void CKbSimDlg::OnOK()
{
	// TODO: Add your specialized code here and/or call the base class

	// CDialogEx::OnOK();
}


void CKbSimDlg::OnCancel()
{
	// TODO: Add your specialized code here and/or call the base class
	OnBnClickedButtonStop();

	CDialogEx::OnCancel();
}
