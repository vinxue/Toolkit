
// IDCardDlg.cpp : implementation file
//

#include "stdafx.h"
#include "IDCard.h"
#include "IDCardDlg.h"
#include "afxdialogex.h"
#include "IDDataBase.h"
#include "IDDataBaseUpdate.h"

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


// CIDCardDlg dialog




CIDCardDlg::CIDCardDlg(CWnd* pParent /*=NULL*/)
	: CDialogEx(CIDCardDlg::IDD, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CIDCardDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CIDCardDlg, CDialogEx)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
    ON_BN_CLICKED(IDC_BUTTON_SEARCH, &CIDCardDlg::OnBnClickedButtonSearch)
    ON_WM_CTLCOLOR()
END_MESSAGE_MAP()


// CIDCardDlg message handlers

BOOL CIDCardDlg::OnInitDialog()
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
    CFont *font;
	font = new CFont;
	font->CreateFont(20, 0, 0, 0, FW_BOLD/*FW_MEDIUM*//*FW_NORMAL*/, FALSE, FALSE, 0, DEFAULT_CHARSET,
		OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH, NULL);
	GetDlgItem(IDC_STATIC_IDSEARCH)->SetFont(font);

    // Set WS_EX_LAYERED on this window 
	SetWindowLong(GetSafeHwnd(), 
		GWL_EXSTYLE, 
		GetWindowLong(GetSafeHwnd(), GWL_EXSTYLE) | WS_EX_LAYERED);

	// Make this window 90% alpha
	::SetLayeredWindowAttributes(GetSafeHwnd(), 0, (255 * 90) / 100, LWA_ALPHA);

	return TRUE;  // return TRUE  unless you set the focus to a control
}

void CIDCardDlg::OnSysCommand(UINT nID, LPARAM lParam)
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

void CIDCardDlg::OnPaint()
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
HCURSOR CIDCardDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}

static bool ValidateDate(int y, int m, int d)
{
    int a[] = { 31, (y % 4 == 0 && y % 100 != 0 || y % 400 == 0) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
    return m >= 1 && m <= 12 && d >= 1 && d <= a[m - 1];
}

BOOL CIDCardDlg::IDCardVerify(CString IDNumStr)
{
    CString strBirthDate;
    int     IdYear;
    int     IdMonth;
    int     IdDay;
    int     Index;
    int     Sum = 0;

    if (IDNumStr.IsEmpty())
	{
		AfxMessageBox(L"The ID Card number not exist.\nPlease check and try again.");
		return FALSE;
	}

    if ((IDNumStr.GetLength() != 15) && (IDNumStr.GetLength() != 18))
    {
        AfxMessageBox(L"The ID Card number length is invalid.");
		return FALSE;
    }

    strBirthDate = IDNumStr.Mid(6, 8); // Birhtdate from 6 to 13
    IdYear  = _ttoi(strBirthDate.Left(4));
    IdMonth = _ttoi(strBirthDate.Mid(4, 2));
    IdDay   = _ttoi(strBirthDate.Right(2));

    if (IDNumStr.GetLength() == 15)
    {
        strBirthDate = IDNumStr.Mid(6, 6); // Birhtdate from 6 to 13
        IdYear  = 1900 + _ttoi(strBirthDate.Left(2));
        IdMonth = _ttoi(strBirthDate.Mid(2, 2));
        IdDay   = _ttoi(strBirthDate.Right(2));
    }

    SYSTEMTIME systime;
    GetLocalTime(&systime);

    if ((IdYear > systime.wYear) || !ValidateDate(IdYear, IdMonth, IdDay))
    {
        AfxMessageBox(L"BirthDate is invalid.");
        return FALSE;
    }

    if (IDNumStr.GetLength() == 18)
    {
        // Verify checksum only for eighteen ID number
        int Factor[] = {7,9,10,5,8,4,2,1,6,3,7,9,10,5,8,4,2};  
        CString VerifyCode = L"10X98765432";

        for (Index = 0; Index < IDNumStr.GetLength() - 1; Index++)
        {
            Sum += ((_ttoi(IDNumStr.Mid(Index, 1)) * Factor[Index]));
        }

        if (VerifyCode.Mid(Sum%11, 1) != IDNumStr.Mid(IDNumStr.GetLength() - 1, 1))
        {
            AfxMessageBox(L"Verifycode is invalid.");
            return FALSE;
        }
    }

    return TRUE;
}

BOOL CIDCardDlg::QueryIDCardInfo(CString IDNumStr)
{
    int     Index;
    int     IdAddr;
    CString strAddr;
    CString strBirthDate;
    CString strGender;
    CString NewIDNumStr;
    int     Sum = 0;

    NewIDNumStr = IDNumStr;

    if (IDNumStr.GetLength() == 15)
    {
        NewIDNumStr = IDNumStr.Left(6) + L"19" + IDNumStr.Right(9);

        // Get Verify Code
        int Factor[] = {7,9,10,5,8,4,2,1,6,3,7,9,10,5,8,4,2};  
        CString VerifyCode = L"10X98765432";

        for (Index = 0; Index < 17; Index++)
        {
            Sum += ((_ttoi(NewIDNumStr.Mid(Index, 1)) * Factor[Index]));
        }

        NewIDNumStr += VerifyCode.Mid(Sum%11, 1);
    }

    IdAddr = _ttoi(NewIDNumStr.Left(6));

    for (Index = 0; Index < sizeof(mIdCard_Addr_Update)/sizeof(mIdCard_Addr_Update[0]); Index++)
    {
        if (IdAddr == mIdCard_Addr_Update[Index].IdAddrNum)
        {
            strAddr = mIdCard_Addr_Update[Index].IdAddr;
            // Show IDCard Address
            ((CStatic*)GetDlgItem(IDC_STATIC_ADDR))->SetWindowText(strAddr);

            // Show IDCard BirthDate
            strBirthDate = NewIDNumStr.Mid(6, 4) + L"年" + NewIDNumStr.Mid(10, 2) + L"月" + NewIDNumStr.Mid(12, 2) + L"日";
            ((CStatic*)GetDlgItem(IDC_STATIC_BIRTHDATE))->SetWindowText(strBirthDate);

            //Show IDCard Gender
            if (_ttoi(NewIDNumStr.Mid(16, 1)) %2)  //Odd number for Male; Even number for Female
            {
                strGender = L"男";
                ((CStatic*)GetDlgItem(IDC_STATIC_GENDER))->SetWindowText(strGender);
            }
            else
            {
                strGender = L"女";
                ((CStatic*)GetDlgItem(IDC_STATIC_GENDER))->SetWindowText(strGender);
            }

            // Show Eighteen ID Number
            ((CStatic*)GetDlgItem(IDC_STATIC_EIGHTEENID))->SetWindowText(NewIDNumStr);
            return TRUE;
        }
    }

    for (Index = 0; Index < sizeof(mIdCard_Addr)/sizeof(mIdCard_Addr[0]); Index++)
    {
        if (IdAddr == mIdCard_Addr[Index].IdAddrNum)
        {
            strAddr = mIdCard_Addr[Index].IdAddr;
            // Show IDCard Address
            ((CStatic*)GetDlgItem(IDC_STATIC_ADDR))->SetWindowText(strAddr);

            // Show IDCard BirthDate
            strBirthDate = NewIDNumStr.Mid(6, 4) + L"年" + NewIDNumStr.Mid(10, 2) + L"月" + NewIDNumStr.Mid(12, 2) + L"日";
            ((CStatic*)GetDlgItem(IDC_STATIC_BIRTHDATE))->SetWindowText(strBirthDate);

            //Show IDCard Gender
            if (_ttoi(NewIDNumStr.Mid(16, 1)) %2)  //Odd number for Male; Even number for Female
            {
                strGender = L"男";
                ((CStatic*)GetDlgItem(IDC_STATIC_GENDER))->SetWindowText(strGender);
            }
            else
            {
                strGender = L"女";
                ((CStatic*)GetDlgItem(IDC_STATIC_GENDER))->SetWindowText(strGender);
            }

            // Show Eighteen ID Number
            ((CStatic*)GetDlgItem(IDC_STATIC_EIGHTEENID))->SetWindowText(NewIDNumStr);
            return TRUE;
        }
    }

    AfxMessageBox(L"Cannot query IDCard info.");
    return FALSE;
}

void CIDCardDlg::OnBnClickedButtonSearch()
{
    // TODO: Add your control notification handler code here
    CString strIdNum;
    CEdit *pEditSrc = (CEdit *)GetDlgItem(IDC_EDIT_IDNUM);
	pEditSrc->GetWindowTextW(strIdNum);

	// Step 1: Verify the IDCard Number whether is valid
    if (!IDCardVerify(strIdNum))
        return;

    // Step2. Query IDCard Info
    if (!QueryIDCardInfo(strIdNum))
        return;
}


BOOL CIDCardDlg::PreTranslateMessage(MSG* pMsg)
{
    // TODO: Add your specialized code here and/or call the base class
    // Disable Esc key
    if (WM_KEYDOWN == pMsg->message)
    {
        UINT nKey = (int) pMsg->wParam;
        if(/*VK_RETURN == nKey || */VK_ESCAPE == nKey)
        {
            return TRUE;
        }

        if (VK_RETURN == nKey)
        {
            OnBnClickedButtonSearch();
            return TRUE;
        }
    }
    // Disable Alt+F4 combine key
    else if (WM_SYSKEYDOWN == pMsg->message)
    {
        UINT nKey = (int) pMsg->wParam;

        if (VK_F4 == nKey)
        {
            //Eliminate Alt+F4
            if (::GetKeyState(VK_MENU) < 0)
            {
                return TRUE;
            }
        }
    }
    // Show About dialog when double click user icon
    else if (WM_LBUTTONDOWN/*WM_LBUTTONDBLCLK*/ == pMsg->message)
    {
        POINT MousePoint;
        GetCursorPos(&MousePoint);
        HANDLE hwnd = WindowFromPoint(MousePoint);
        if (hwnd == GetDlgItem(IDC_STATIC_USER))
        {
            CAboutDlg dlgAbout;
            dlgAbout.DoModal();
        }
    }

    return CDialogEx::PreTranslateMessage(pMsg);
}


HBRUSH CIDCardDlg::OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor)
{
    HBRUSH hbr = CDialogEx::OnCtlColor(pDC, pWnd, nCtlColor);

    // TODO:  Change any attributes of the DC here

    // TODO:  Return a different brush if the default is not desired
    if (pWnd->GetDlgCtrlID() == IDC_STATIC_IDSEARCH)
	{
		//HBRUSH hbr = CreateSolidBrush(RGB(255, 255, 2555));
		pDC->SetTextColor(RGB(0, 102, 129));
		pDC-> SetBkMode(TRANSPARENT);
		return hbr;
	}
    return hbr;
}
