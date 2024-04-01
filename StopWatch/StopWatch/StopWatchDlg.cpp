// StopWatchDlg.cpp : implementation file
//

#include "stdafx.h"
#include "StopWatch.h"
#include "StopWatchDlg.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif


// CAboutDlg dialog used for App About
int hour, minute, second, ms;
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


// CStopWatchDlg dialog




CStopWatchDlg::CStopWatchDlg(CWnd* pParent /*=NULL*/)
	: CDialog(CStopWatchDlg::IDD, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CStopWatchDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialog::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_START, m_cStart);
	DDX_Control(pDX, IDC_CONTINUE, m_cContinue);
	DDX_Control(pDX, IDC_STOP, m_cStop);
	DDX_Control(pDX, IDC_CLEAR, m_cClear);
	DDX_Control(pDX, IDC_DISPLAY, m_cDisplay);
	DDX_Control(pDX, IDC_BUTTON_LAP, m_cLap);
	DDX_Control(pDX, IDC_STATIC_LAPDISPLAY, m_cLapDisplay);
	DDX_Control(pDX, IDC_LIST1, m_cList);
}

BEGIN_MESSAGE_MAP(CStopWatchDlg, CDialog)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	//}}AFX_MSG_MAP
	ON_WM_TIMER()
	//ON_WM_CTLCOLOR()
	ON_BN_CLICKED(IDC_START, &CStopWatchDlg::OnBnClickedStart)
	ON_BN_CLICKED(IDC_STOP, &CStopWatchDlg::OnBnClickedStop)
	ON_BN_CLICKED(IDC_CLEAR, &CStopWatchDlg::OnBnClickedClear)
	ON_BN_CLICKED(IDC_CONTINUE, &CStopWatchDlg::OnBnClickedContinue)
	ON_BN_CLICKED(IDC_BUTTON_LAP, &CStopWatchDlg::OnBnClickedButtonLap)
END_MESSAGE_MAP()


// CStopWatchDlg message handlers

BOOL CStopWatchDlg::OnInitDialog()
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
	m_cStart.ShowWindow(SW_SHOW);
	m_cStop.ShowWindow(SW_HIDE);
	m_cContinue.ShowWindow(SW_HIDE);
	m_cLap.ShowWindow(SW_HIDE);
	m_cClear.ShowWindow(SW_SHOW);
	m_cLapDisplay.ShowWindow(SW_HIDE);//EnableWindow(FALSE);
	m_cList.ShowWindow(SW_HIDE);

	CFont *font;
    font=new CFont;
	font->CreateFont(50, 0, 0, 0, FW_NORMAL, FALSE, FALSE, 0, DEFAULT_CHARSET,
		OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH, _T("Arial"));
	GetDlgItem(IDC_DISPLAY)->SetFont(font);

	CString str;
	CStatic* st=(CStatic*)GetDlgItem(IDC_DISPLAY);
	str.Format(_T("%02d:%02d:%02d.%02d"), 0, 0, 0, 0);
	st->SetWindowText(str);
	//SetDlgItemText(IDC_DISPLAY,str);

	//SetTimer(2,10,NULL);

	return TRUE;  // return TRUE  unless you set the focus to a control
}

void CStopWatchDlg::OnSysCommand(UINT nID, LPARAM lParam)
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

void CStopWatchDlg::OnPaint()
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
HCURSOR CStopWatchDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}

long t;
void CStopWatchDlg::OnBnClickedStart()
{
	// TODO: Add your control notification handler code here
	check = TRUE;
	m_cStart.ShowWindow(SW_HIDE);
	m_cStop.ShowWindow(SW_SHOW);
	m_cClear.ShowWindow(SW_HIDE);
	m_cLap.ShowWindow(SW_SHOW);
    SetTimer(1,20,NULL);
    t=GetTickCount();
}

void CStopWatchDlg::OnTimer(UINT nIDEvent) 
{
	// TODO: Add your message handler code here and/or call default
	if(nIDEvent==1)
	{
		if(check == TRUE)
			stopwatch();
	}

	/*if(nIDEvent==2)
	{
		KillTimer(2);
	 }*/

	CDialog::OnTimer(nIDEvent);
}

void CStopWatchDlg::OnBnClickedStop()
{
	// TODO: Add your control notification handler code here
	check=FALSE;
	m_cClear.ShowWindow(SW_SHOW);
    m_cStop.ShowWindow(SW_HIDE);
	m_cContinue.ShowWindow(SW_SHOW);
	m_cLap.ShowWindow(SW_HIDE);
	CString str;
	CStatic* st=(CStatic*)GetDlgItem(IDC_DISPLAY);

	str.Format(_T("%02d:%02d:%02d.%02d"), hour, minute, second, ms);
	st->SetWindowText(str);
}

void CStopWatchDlg::OnBnClickedClear()
{
	// TODO: Add your control notification handler code here
	KillTimer(1);
	t=0;

    CString str;
	CStatic* st=(CStatic*)GetDlgItem(IDC_DISPLAY);
	str.Format(_T("%02d:%02d:%02d.%02d"), t, t, t, t);
	st->SetWindowText(str);
	SetDlgItemText(IDC_STATIC_LAPDISPLAY, str);
	
	m_cList.ResetContent();

	m_cLapDisplay.ShowWindow(SW_HIDE);
	m_cStart.ShowWindow(SW_SHOW);
	m_cStop.ShowWindow(SW_HIDE);
	m_cContinue.ShowWindow(SW_HIDE);
	m_cList.ShowWindow(SW_HIDE);
}

void CStopWatchDlg::stopwatch()
{
	CString str;
	CStatic* st=(CStatic*)GetDlgItem(IDC_DISPLAY);
	hour=(GetTickCount()-t)/3600000;
	minute=((GetTickCount()-t)%3600000)/60000;
	second=((GetTickCount()-t)%3600000)%60000/1000;
	ms=((GetTickCount()-t)%3600000)%60000%1000/10;

    str.Format(_T("%02d:%02d:%02d.%02d"), hour, minute, second, ms);
	st->SetWindowText(str);
}


void CStopWatchDlg::OnBnClickedContinue()
{
	// TODO: Add your control notification handler code here
	check=TRUE;
	m_cContinue.ShowWindow(SW_HIDE);
	m_cStop.ShowWindow(SW_SHOW);
	m_cLap.ShowWindow(SW_SHOW);
	m_cClear.ShowWindow(SW_HIDE);
	CString str;
	CStatic* st=(CStatic*)GetDlgItem(IDC_DISPLAY);

    str.Format(_T("%02d:%02d:%02d.%02d"), hour, minute, second, ms);
	st->SetWindowText(str);
}

/*HBRUSH CStopWatchDlg::OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor) 
{
	HBRUSH hbr = CDialog::OnCtlColor(pDC, pWnd, nCtlColor);
	
	// TODO: Change any attributes of the DC here
	if(nCtlColor==CTLCOLOR_STATIC)
	{
		HBRUSH B = CreateSolidBrush(RGB(185, 255, 185));
		pDC->SetTextColor(RGB(0, 0, 255));
		pDC->SetBkColor(RGB(255, 255, 255));
		return B;
	}
	if(nCtlColor==CTLCOLOR_DLG)
	{
		HBRUSH b=CreateSolidBrush(RGB(255, 255, 255));
		return b;
	}

	// TODO: Return a different brush if the default is not desired
	return hbr;
}*/
void CStopWatchDlg::OnBnClickedButtonLap()
{
	// TODO: Add your control notification handler code here
	m_cLap.ShowWindow(SW_SHOW);
	m_cLapDisplay.ShowWindow(SW_SHOW);
	m_cList.ShowWindow(SW_SHOW);

	CString str;
    str.Format(_T("%02d:%02d:%02d.%02d"), hour, minute, second, ms);
	SetDlgItemText(IDC_STATIC_LAPDISPLAY, str);
	m_cList.AddString(str);
}
