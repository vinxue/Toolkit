
// BitViewerDlg.cpp : implementation file
//

#include "pch.h"
#include "framework.h"
#include "BitViewer.h"
#include "BitViewerDlg.h"
#include "afxdialogex.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif

#define  BIT0                   0x00000001
#define  INPUT_HEX_DATA_LENGTH  16
#define  INPUT_BITFIELD_LENGTH  2

// CAboutDlg dialog used for App About

class CAboutDlg : public CDialogEx
{
public:
	CAboutDlg();

// Dialog Data
#ifdef AFX_DESIGN_TIME
	enum { IDD = IDD_ABOUTBOX };
#endif

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);    // DDX/DDV support

// Implementation
protected:
	DECLARE_MESSAGE_MAP()
};

CAboutDlg::CAboutDlg() : CDialogEx(IDD_ABOUTBOX)
{
}

void CAboutDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CAboutDlg, CDialogEx)
END_MESSAGE_MAP()


// CBitViewerDlg dialog



CBitViewerDlg::CBitViewerDlg(CWnd* pParent /*=nullptr*/)
	: CDialogEx(IDD_BITVIEWER_DIALOG, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CBitViewerDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CBitViewerDlg, CDialogEx)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	ON_BN_CLICKED(IDC_BUTTON_DECODE, &CBitViewerDlg::OnBnClickedButtonDecode)
	ON_WM_CTLCOLOR()
	ON_BN_CLICKED(IDOK, &CBitViewerDlg::OnBnClickedOk)
	ON_BN_CLICKED(IDC_CHECK_SET_BITFIELD, &CBitViewerDlg::OnBnClickedCheckSetBitfield)
	ON_BN_CLICKED(IDC_BUTTON_SET_BITFIELD, &CBitViewerDlg::OnBnClickedButtonSetBitfield)
END_MESSAGE_MAP()


// CBitViewerDlg message handlers

BOOL CBitViewerDlg::OnInitDialog()
{
	CDialogEx::OnInitDialog();

	// Add "About..." menu item to system menu.

	// IDM_ABOUTBOX must be in the system command range.
	ASSERT((IDM_ABOUTBOX & 0xFFF0) == IDM_ABOUTBOX);
	ASSERT(IDM_ABOUTBOX < 0xF000);

	CMenu* pSysMenu = GetSystemMenu(FALSE);
	if (pSysMenu != nullptr)
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
	CFont*          Font;
	UINT8           Index;
	CString         Str;

	Font = new CFont;
	Font->CreateFont(26, 0, 0, 0, FW_BOLD, FALSE, FALSE, 0, ANSI_CHARSET,
		OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH | FF_SWISS, _T("Arial"));
	GetDlgItem(IDC_STATIC_TITLE)->SetFont(Font);

	// Set WS_EX_LAYERED on this window
	SetWindowLong(GetSafeHwnd(),
		GWL_EXSTYLE,
		GetWindowLong(GetSafeHwnd(), GWL_EXSTYLE) | WS_EX_LAYERED);

	// Make this window 95% alpha
	::SetLayeredWindowAttributes(GetSafeHwnd(), 0, (255 * 95) / 100, LWA_ALPHA);

	//
	// Set the text limit for input data
	//
	((CEdit*)GetDlgItem(IDC_EDIT_DATA))->SetLimitText(INPUT_HEX_DATA_LENGTH);
	((CEdit*)GetDlgItem(IDC_EDIT_STARTBIT))->SetLimitText(INPUT_BITFIELD_LENGTH);
	((CEdit*)GetDlgItem(IDC_EDIT_ENDBIT))->SetLimitText(INPUT_BITFIELD_LENGTH);
	((CEdit*)GetDlgItem(IDC_EDIT_BITFIELD_VALUE))->SetLimitText(INPUT_HEX_DATA_LENGTH);

	//
	// Set text of bitfield
	//
	for (Index = 0; Index < sizeof(UINT64) * 8; Index++)
	{
		Str.Format(L"%d", Index);
		((CStatic*)GetDlgItem(IDC_STATIC_BITFIELD0 + Index))->SetWindowText(Str);
	}

	//
	// Set default start/end bit to 0
	//
	Str.Format(L"%d", 0);
	((CStatic*)GetDlgItem(IDC_EDIT_STARTBIT))->SetWindowText(Str);
	((CStatic*)GetDlgItem(IDC_EDIT_ENDBIT))->SetWindowText(Str);


	//
	// Set default check box
	//
	((CButton*)GetDlgItem(IDC_CHECK_SET_BITFIELD))->SetCheck(FALSE);
	((CButton*)GetDlgItem(IDC_BUTTON_SET_BITFIELD))->EnableWindow(FALSE);
	((CEdit*)GetDlgItem(IDC_EDIT_BITFIELD_VALUE))->EnableWindow(FALSE);

	return TRUE;  // return TRUE  unless you set the focus to a control
}

void CBitViewerDlg::OnSysCommand(UINT nID, LPARAM lParam)
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

void CBitViewerDlg::OnPaint()
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
HCURSOR CBitViewerDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}


UINT64 RShiftU64(UINT64 Operand, UINT64 Count)
{
	ASSERT(Count < 64);
	return Operand >> Count;
}

UINT64 LShiftU64(UINT64 Operand, UINT64 Count)
{
	ASSERT(Count < 64);
	return Operand << Count;
}

UINT64 BitFieldOr64(UINT64 Operand, UINT64 StartBit, UINT64 EndBit, UINT64 OrData)
{
	UINT64  Value1;
	UINT64  Value2;

	ASSERT(EndBit < 64);
	ASSERT(StartBit <= EndBit);
	//
	// Higher bits in OrData those are not used must be zero.
	//
	// EndBit - StartBit + 1 might be 64 while the result right shifting 64 on RShiftU64() API is invalid,
	// So the logic is updated to right shift (EndBit - StartBit) bits and compare the last bit directly.
	//
	ASSERT(RShiftU64(OrData, EndBit - StartBit) == (RShiftU64(OrData, EndBit - StartBit) & 1));

	Value1 = LShiftU64(OrData, StartBit);
	Value2 = LShiftU64((UINT64)-2, EndBit);

	return Operand | (Value1 & ~Value2);
}

UINT64 BitFieldAnd64(UINT64 Operand, UINT64 StartBit, UINT64 EndBit, UINT64 AndData)
{
	UINT64  Value1;
	UINT64  Value2;

	ASSERT(EndBit < 64);
	ASSERT(StartBit <= EndBit);
	//
	// Higher bits in AndData those are not used must be zero.
	//
	// EndBit - StartBit + 1 might be 64 while the right shifting 64 on RShiftU64() API is invalid,
	// So the logic is updated to right shift (EndBit - StartBit) bits and compare the last bit directly.
	//
	ASSERT(RShiftU64(AndData, EndBit - StartBit) == (RShiftU64(AndData, EndBit - StartBit) & 1));

	Value1 = LShiftU64(~AndData, StartBit);
	Value2 = LShiftU64((UINT64)-2, EndBit);

	return Operand & ~(Value1 & ~Value2);
}

UINT64 BitFieldAndThenOr64(UINT64 Operand, UINT64 StartBit, UINT64 EndBit, UINT64 AndData, UINT64 OrData)
{
	ASSERT(EndBit < 64);
	ASSERT(StartBit <= EndBit);
	return BitFieldOr64(
		BitFieldAnd64(Operand, StartBit, EndBit, AndData),
		StartBit,
		EndBit,
		OrData
	);
}

UINT64 BitFieldRead64(UINT64 Operand, UINT64 StartBit, UINT64 EndBit)
{
	ASSERT(EndBit < 64);
	ASSERT(StartBit <= EndBit);
	return RShiftU64(Operand & ~LShiftU64((UINT64)-2, EndBit), StartBit);
}

UINT64 BitFieldWrite64(UINT64 Operand, UINT64 StartBit, UINT64 EndBit, UINT64 Value)
{
	ASSERT(EndBit < 64);
	ASSERT(StartBit <= EndBit);
	return BitFieldAndThenOr64(Operand, StartBit, EndBit, 0, Value);
}


void CBitViewerDlg::OnBnClickedButtonDecode()
{
	// TODO: Add your control notification handler code here
	CEdit*               CEditCtrl;
	UINT64               InputData;
	CString              Str;
	CString              StrStartBit;
	CString              StrEndBit;
	UINT8                Index;
	UINT8                BitData;
	UINT8                BitsData;
	UINT8                StartBit;
	UINT8                EndBit;
	UINT64               BitFieldVal;

	UpdateData(TRUE);

	//
	// Get input hex data
	//
	CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_DATA);
	CEditCtrl->GetWindowText(Str);
	if (CEditCtrl->GetWindowTextLength() == 0)
	{
		AfxMessageBox(L"Please input a hex data.\n");
		return;
	}

	InputData = _wcstoui64(Str, NULL, 16);

	//
	// Get Start/End bit
	//
	((CEdit*)GetDlgItem(IDC_EDIT_STARTBIT))->GetWindowText(StrStartBit);
	((CEdit*)GetDlgItem(IDC_EDIT_ENDBIT))->GetWindowText(StrEndBit);

	StartBit = (UINT8)wcstoul(StrStartBit, NULL, 10);
	EndBit = (UINT8)wcstoul(StrEndBit, NULL, 10);

	if ((StartBit > 63) || (EndBit > 63) || (StartBit > EndBit))
	{
		AfxMessageBox(L"Please input a valid Start/End Bit.\n");
		return;
	}

	for (Index = 0; Index < sizeof(UINT64) * 8; Index++)
	{
		CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_BIT0 + Index);
		BitData = (InputData & ((UINT64)BIT0 << Index)) ? 1 : 0;

		Str.Format(L"%X", BitData);
		((CEdit*)GetDlgItem(IDC_EDIT_BIT0 + Index))->SetWindowText(Str);
	}

	for (Index = 0; Index < sizeof(UINT64) * 2; Index++)
	{
		BitsData = (InputData >> (Index * 4)) & 0xF;

		Str.Format(L"%X", BitsData);
		((CStatic*)GetDlgItem(IDC_STATIC_BITS0 + Index))->SetWindowText(Str);
	}

	//
	// Display bitfield value
	//
	BitFieldVal = BitFieldRead64(InputData, StartBit, EndBit);
	Str.Format(L"%llX", BitFieldVal);
	((CStatic*)GetDlgItem(IDC_EDIT_BITFIELD_VALUE))->SetWindowText(Str);
}


HBRUSH CBitViewerDlg::OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor)
{
	HBRUSH hbr = CDialogEx::OnCtlColor(pDC, pWnd, nCtlColor);

	// TODO:  Change any attributes of the DC here
	UINT8           Index;

	if (pWnd->GetDlgCtrlID() == IDC_STATIC_TITLE)
	{
		pDC->SetTextColor(RGB(0, 0x6B, 0xB5));
		pDC->SetBkMode(TRANSPARENT);
		return hbr;
	}

	for (Index = 0; Index < sizeof(UINT64) * 2; Index++)
	{
		if (pWnd->GetDlgCtrlID() == (IDC_STATIC_BITS0 + Index))
		{
			pDC->SetTextColor(RGB(0, 0x6B, 0xB5));
			pDC->SetBkMode(TRANSPARENT);
			return hbr;
		}
	}

	// TODO:  Return a different brush if the default is not desired
	return hbr;
}


BOOL CBitViewerDlg::PreTranslateMessage(MSG* pMsg)
{
	// TODO: Add your specialized code here and/or call the base class
	// Disable Esc key
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
	else if (WM_CHAR == pMsg->message)
	{
		if (GetDlgItem(IDC_EDIT_DATA) == GetFocus() || GetDlgItem(IDC_EDIT_BITFIELD_VALUE) == GetFocus())
		{
			SHORT Keystate = GetKeyState(VK_CONTROL);
			// Check input hex string
			if ((pMsg->wParam >= 0x30 && pMsg->wParam <= 0x39) ||
				(pMsg->wParam >= 'a' && pMsg->wParam <= 'f') ||
				(pMsg->wParam >= 'A' && pMsg->wParam <= 'F') ||
				(pMsg->wParam == 0x08) || (pMsg->wParam == 0x7F) ||  // Backspace || Delete
				(GetKeyState(VK_CONTROL) & 0x8000))  // Ctrl
			{
				return CDialogEx::PreTranslateMessage(pMsg);
			}
			else
			{
				pMsg->wParam = NULL;
			}
		}
	}

	return CDialogEx::PreTranslateMessage(pMsg);
}


void CBitViewerDlg::OnBnClickedOk()
{
	// TODO: Add your control notification handler code here
	CBitViewerDlg::OnBnClickedButtonDecode();
	//CDialogEx::OnOK();
}


void CBitViewerDlg::OnBnClickedCheckSetBitfield()
{
	// TODO: Add your control notification handler code here
	if (((CButton*)GetDlgItem(IDC_CHECK_SET_BITFIELD))->GetCheck())
	{
		((CButton*)GetDlgItem(IDC_BUTTON_SET_BITFIELD))->EnableWindow(TRUE);
		((CEdit*)GetDlgItem(IDC_EDIT_BITFIELD_VALUE))->EnableWindow(TRUE);
	}
	else
	{
		((CButton*)GetDlgItem(IDC_BUTTON_SET_BITFIELD))->EnableWindow(FALSE);
		((CEdit*)GetDlgItem(IDC_EDIT_BITFIELD_VALUE))->EnableWindow(FALSE);
	}
}


void CBitViewerDlg::OnBnClickedButtonSetBitfield()
{
	// TODO: Add your control notification handler code here
	CEdit                *CEditCtrl;
	UINT64               InputData;
	CString              Str;
	CString              StrStartBit;
	CString              StrEndBit;
	UINT8                Index;
	UINT8                BitData;
	UINT8                BitsData;
	UINT8                StartBit;
	UINT8                EndBit;
	CString              StrBitField;
	UINT64               NewBitField;
	UINT64               NewInputData;

	UpdateData(TRUE);

	//
	// Get input hex data
	//
	CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_DATA);
	CEditCtrl->GetWindowText(Str);
	if (CEditCtrl->GetWindowTextLength() == 0)
	{
		AfxMessageBox(L"Please input a hex data.\n");
		return;
	}

	InputData = _wcstoui64(Str, NULL, 16);

	//
	// Get Start/End bit
	//
	((CEdit*)GetDlgItem(IDC_EDIT_STARTBIT))->GetWindowText(StrStartBit);
	((CEdit*)GetDlgItem(IDC_EDIT_ENDBIT))->GetWindowText(StrEndBit);

	StartBit = (UINT8)wcstoul(StrStartBit, NULL, 10);
	EndBit = (UINT8)wcstoul(StrEndBit, NULL, 10);

	if ((StartBit > 63) || (EndBit > 63) || (StartBit > EndBit))
	{
		AfxMessageBox(L"Please input a valid Start/End Bit.\n");
		return;
	}

	//
	// Get new value of the bit field
	//
	((CEdit*)GetDlgItem(IDC_EDIT_BITFIELD_VALUE))->GetWindowText(StrBitField);
	NewBitField = _wcstoui64(StrBitField, NULL, 16);

	if (RShiftU64(NewBitField, EndBit - StartBit) != (RShiftU64(NewBitField, EndBit - StartBit) & 1))
	{
		AfxMessageBox(L"Please input a valid new value of bit field.\n");
		return;
	}

	NewInputData = BitFieldWrite64(_wcstoui64(Str, NULL, 16), StartBit, EndBit, NewBitField);

	//
	// Update the new data
	//
	Str.Format(L"%llx", NewInputData);
	((CEdit*)GetDlgItem(IDC_EDIT_DATA))->SetWindowText(Str);

	for (Index = 0; Index < sizeof(UINT64) * 8; Index++)
	{
		CEditCtrl = (CEdit*)GetDlgItem(IDC_EDIT_BIT0 + Index);
		BitData = (NewInputData & ((UINT64)BIT0 << Index)) ? 1 : 0;

		Str.Format(L"%X", BitData);
		((CEdit*)GetDlgItem(IDC_EDIT_BIT0 + Index))->SetWindowText(Str);
	}

	for (Index = 0; Index < sizeof(UINT64) * 2; Index++)
	{
		BitsData = (NewInputData >> (Index * 4)) & 0xF;

		Str.Format(L"%X", BitsData);
		((CStatic*)GetDlgItem(IDC_STATIC_BITS0 + Index))->SetWindowText(Str);
	}
}
