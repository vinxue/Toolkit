
// KbSimDlg.cpp : implementation file
//

#include "stdafx.h"
#include "KbSim.h"
#include "KbSimDlg.h"
#include "afxdialogex.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif


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

	((CButton*)GetDlgItem(IDC_BUTTON_STOP))->ShowWindow(FALSE);

	CFont* font;
	font = new CFont;
	font->CreateFont(16, 0, 0, 0, FW_BOLD/*FW_MEDIUM*//*FW_NORMAL*/, FALSE, FALSE, 0, DEFAULT_CHARSET,
		OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH, _T("Arial"));
	GetDlgItem(IDC_STATIC_KbSim)->SetFont(font);

	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->SetLimitText(3);
	Str.Format(L"%d", 60);
	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->SetWindowText(Str);

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



void CKbSimDlg::OnTimer(UINT_PTR nIDEvent)
{
	// TODO: Add your message handler code here and/or call default
	keybd_event(VK_CAPITAL, 0, 0, 0);
	keybd_event(VK_CAPITAL, 0, KEYEVENTF_KEYUP, 0);

	CDialogEx::OnTimer(nIDEvent);
}



void CKbSimDlg::OnBnClickedButtonRun()
{
	// TODO: Add your control notification handler code here
	CString StrIntervalTime;
	UINT32 IntervalTime;

	// Get interval time
	((CEdit*)GetDlgItem(IDC_EDIT_INTERVAL))->GetWindowText(StrIntervalTime);
	IntervalTime = wcstoul(StrIntervalTime, NULL, 10);

	if ((IntervalTime < 1) || (IntervalTime > 299))
	{
		AfxMessageBox(L"Please input a valid value (1 ~ 299)\n");
		return;
	}

	SetTimer(0, IntervalTime * 1000, NULL);

	((CButton*)GetDlgItem(IDC_BUTTON_RUN))->ShowWindow(FALSE);
	((CButton*)GetDlgItem(IDC_BUTTON_STOP))->ShowWindow(TRUE);
}


void CKbSimDlg::OnBnClickedButtonStop()
{
	// TODO: Add your control notification handler code here
	KillTimer(0);

	if (LOBYTE(GetKeyState(VK_CAPITAL)))
	{
		keybd_event(VK_CAPITAL, 0, 0, 0);
		keybd_event(VK_CAPITAL, 0, KEYEVENTF_KEYUP, 0);
	}

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
