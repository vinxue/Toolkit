
// PCIeDlg.cpp : implementation file
//

#include "stdafx.h"
#include "PCIe.h"
#include "PCIeDlg.h"
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


// CPCIeDlg dialog



CPCIeDlg::CPCIeDlg(CWnd* pParent /*=NULL*/)
	: CDialogEx(CPCIeDlg::IDD, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CPCIeDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CPCIeDlg, CDialogEx)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
  ON_BN_CLICKED(IDC_BUTTON_DONE, &CPCIeDlg::OnBnClickedButtonDone)
  ON_BN_CLICKED(IDC_BUTTON_CLEAR, &CPCIeDlg::OnBnClickedButtonClear)
  ON_WM_CTLCOLOR()
END_MESSAGE_MAP()


// CPCIeDlg message handlers

BOOL CPCIeDlg::OnInitDialog()
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
  CString Str;
  Str.Format(L"%02X", 0);
  (((CEdit*)GetDlgItem(IDC_EDIT_BUS)))->SetWindowTextW(Str);
  (((CEdit*)GetDlgItem(IDC_EDIT_FUN)))->SetWindowTextW(Str);
  (((CEdit*)GetDlgItem(IDC_EDIT_DEV)))->SetWindowTextW(Str);
  (((CEdit*)GetDlgItem(IDC_EDIT_OFFSET)))->SetWindowTextW(Str);

  CFont *font;
  font = new CFont;

  font->CreateFont(16, 0, 0, 0, /*FW_BOLD*//*FW_MEDIUM*/FW_NORMAL, FALSE, FALSE, 0, DEFAULT_CHARSET,
    OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH, _T("Arial"));
  GetDlgItem(IDC_STATIC_PCIEADDR)->SetFont(font);
  GetDlgItem(IDC_STATIC_PCIADDR)->SetFont(font);

	return TRUE;  // return TRUE  unless you set the focus to a control
}

void CPCIeDlg::OnSysCommand(UINT nID, LPARAM lParam)
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

void CPCIeDlg::OnPaint()
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
HCURSOR CPCIeDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}

#define PCI_PCIE_ADDR(Bus, Device, Function, Offset) \
  (((Offset) & 0xfff) | (((Function) & 0x07) << 12) | (((Device) & 0x1f) << 15) | (((Bus) & 0xff) << 20))

#define PCI_CF8_ADDR(Bus, Dev, Func, Off) \
  (((Off) & 0xFF) | (((Func) & 0x07) << 8) | (((Dev) & 0x1F) << 11) | (((Bus) & 0xFF) << 16) | (1 << 31))

void CPCIeDlg::OnBnClickedButtonDone()
{
  // TODO: Add your control notification handler code here
  CEdit*       CEditCtrl;
  int          BusNum;
  int          DevNum;
  int          FunNum;
  int          Offset;
  int          PCIeAddr;
  int          PCIAddr;
  CString      Str;

  UpdateData(TRUE);

  CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_BUS);
  CEditCtrl->GetWindowTextW(Str);
  Str = L"0x" + Str;
  StrToIntEx(Str, STIF_SUPPORT_HEX, &BusNum);  

  CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_DEV);
  CEditCtrl->GetWindowTextW(Str);
  Str = L"0x" + Str;
  StrToIntEx(Str, STIF_SUPPORT_HEX, &DevNum);

  CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_FUN);
  CEditCtrl->GetWindowTextW(Str);
  Str = L"0x" + Str;
  StrToIntEx(Str, STIF_SUPPORT_HEX, &FunNum);

  CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_OFFSET);
  CEditCtrl->GetWindowTextW(Str);
  Str = L"0x" + Str;
  StrToIntEx(Str, STIF_SUPPORT_HEX, &Offset);

  if ((BusNum > 0xFF) || (DevNum > 0x1F) || (FunNum > 0x7)) {
    AfxMessageBox(L"Please input a valid data!\n");
    return;
  }

  PCIeAddr = PCI_PCIE_ADDR(BusNum, DevNum, FunNum, Offset);
  PCIAddr = PCI_CF8_ADDR(BusNum, DevNum, FunNum, Offset);

  Str.Format(L"0x%08X", PCIeAddr);
  (((CEdit*)GetDlgItem(IDC_STATIC_PCIEADDR)))->SetWindowTextW(Str);

  Str.Format(L"0x%08X", PCIAddr);
  (((CEdit*)GetDlgItem(IDC_STATIC_PCIADDR)))->SetWindowTextW(Str);
}


BOOL CPCIeDlg::PreTranslateMessage(MSG* pMsg)
{
  // TODO: Add your specialized code here and/or call the base class
  if (WM_KEYDOWN == pMsg->message)
  {
    UINT nKey = (int)pMsg->wParam;
    if (/*VK_RETURN == nKey || */VK_ESCAPE == nKey)
    {
      return TRUE;
    }
  }
  // Disable Alt+F4 combine key
  else if (WM_SYSKEYDOWN == pMsg->message)
  {
    UINT nKey = (int)pMsg->wParam;

    if (VK_F4 == nKey)
    {
      //Eliminate Alt+F4
      if (::GetKeyState(VK_MENU) < 0)
      {
        return TRUE;
      }
    }
  }
  return CDialogEx::PreTranslateMessage(pMsg);
}


void CPCIeDlg::OnOK()
{
  // TODO: Add your specialized code here and/or call the base class
  OnBnClickedButtonDone();
  //CDialogEx::OnOK();
}


void CPCIeDlg::OnBnClickedButtonClear()
{
  // TODO: Add your control notification handler code here
  CString Str;
  Str.Format(L"%02X", 0);
  (((CEdit*)GetDlgItem(IDC_EDIT_BUS)))->SetWindowTextW(Str);
  (((CEdit*)GetDlgItem(IDC_EDIT_FUN)))->SetWindowTextW(Str);
  (((CEdit*)GetDlgItem(IDC_EDIT_DEV)))->SetWindowTextW(Str);
  (((CEdit*)GetDlgItem(IDC_EDIT_OFFSET)))->SetWindowTextW(Str);

  Str.Format(L"0x%08X", 0);
  (((CEdit*)GetDlgItem(IDC_STATIC_PCIEADDR)))->SetWindowTextW(Str);
  (((CEdit*)GetDlgItem(IDC_STATIC_PCIADDR)))->SetWindowTextW(Str);
}


HBRUSH CPCIeDlg::OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor)
{
  HBRUSH hbr = CDialogEx::OnCtlColor(pDC, pWnd, nCtlColor);

  // TODO:  Change any attributes of the DC here

  // TODO:  Return a different brush if the default is not desired
  if ((pWnd->GetDlgCtrlID() == IDC_STATIC_PCIEADDR) || (pWnd->GetDlgCtrlID() == IDC_STATIC_PCIADDR))
  {
    pDC->SetTextColor(RGB(0x0B, 0x61, 0xBB));
    pDC->SetBkMode(TRANSPARENT);
    return hbr;
  }
  return hbr;
}
