
// ImgConverterDlg.cpp : implementation file
//
/*********************************************************
Author: Gavin
Date: 06/04/2011
Version: 1.0.0.3
History: 1.Add a option for user to control the utility is in the top or not.
Known Issue: None
***********************************************************/

/*********************************************************
Author: Gavin
Date: 05/21/2011
Version: 1.0.0.2
History: 1.Fixed the Set Scrollbar Position initial value is not synchronize with "m_PerValue" value.
Known Issue: None
***********************************************************/

/*********************************************************
Author: Gavin
Date: 04/24/2011
Version: 1.0.0.1
History: Initial release
Known Issue: None
***********************************************************/

#include "stdafx.h"
#include "ImgConverter.h"
#include "ImgConverterDlg.h"
#include "afxdialogex.h"
#include <windows.h>
#include <gdiplus.h>
#include <stdio.h>

using namespace Gdiplus;


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
	CPictureEx m_Picture;

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


// CImgConverterDlg dialog




CImgConverterDlg::CImgConverterDlg(CWnd* pParent /*=NULL*/)
	: CDialogEx(CImgConverterDlg::IDD, pParent)
	, m_PerValue(76)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CImgConverterDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
	DDX_Text(pDX, IDC_EDIT_PER, m_PerValue);
	DDV_MinMaxInt(pDX, m_PerValue, 0, 100);
	DDX_Control(pDX, IDC_STATIC_PROGRESS, m_Picture);	//Picture process
}

BEGIN_MESSAGE_MAP(CImgConverterDlg, CDialogEx)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	ON_BN_CLICKED(IDC_BUTTON_SRCBROWSE, &CImgConverterDlg::OnBnClickedButtonSrcbrowse)
	ON_BN_CLICKED(IDC_BUTTON_TGTBROWSE, &CImgConverterDlg::OnBnClickedButtonTgtbrowse)
	ON_BN_CLICKED(IDC_BUTTON_CONVERT, &CImgConverterDlg::OnBnClickedButtonConvert)
	ON_CBN_SELCHANGE(IDC_COMBO_TGTFORMAT, &CImgConverterDlg::OnCbnSelchangeComboTgtformat)
	ON_BN_CLICKED(IDC_CHECK_ADV, &CImgConverterDlg::OnBnClickedCheckAdv)
	ON_WM_HSCROLL()
	ON_EN_CHANGE(IDC_EDIT_PER, &CImgConverterDlg::OnEnChangeEditPer)
	ON_WM_CTLCOLOR()
	ON_BN_CLICKED(IDC_BUTTON_ABOUT, &CImgConverterDlg::OnBnClickedButtonAbout)
	ON_WM_NCHITTEST()
	ON_BN_CLICKED(IDC_BUTTON_SINGLE, &CImgConverterDlg::OnBnClickedButtonSingle)
	ON_BN_CLICKED(IDC_CHECK_TOP, &CImgConverterDlg::OnBnClickedCheckTop)
END_MESSAGE_MAP()


// CImgConverterDlg message handlers

BOOL CImgConverterDlg::OnInitDialog()
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
	font=new CFont;
	font->CreateFont(25, 0, 0, 0, FW_MEDIUM/*FW_NORMAL*/, FALSE, FALSE, 0, DEFAULT_CHARSET,
		OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY, DEFAULT_PITCH, _T("Arial"));
	GetDlgItem(IDC_STATIC_TITLE)->SetFont(font);

	//Ver1.0.0.3 SetWindowPos(&wndTopMost, 0, 0, 0, 0, SWP_FRAMECHANGED|SWP_NOSIZE|SWP_NOMOVE);

	// Set WS_EX_LAYERED on this window 
	SetWindowLong(GetSafeHwnd(), 
		GWL_EXSTYLE, 
		GetWindowLong(GetSafeHwnd(), GWL_EXSTYLE) | WS_EX_LAYERED);

	// Make this window 90% alpha
	::SetLayeredWindowAttributes(GetSafeHwnd(), 0, (255 * 90) / 100, LWA_ALPHA);

	CComboBox* ComboCtrl_Src = (CComboBox*)GetDlgItem(IDC_COMBO_SRCFORMAT);
	ComboCtrl_Src->AddString(L"BMP");
	ComboCtrl_Src->AddString(L"JPG");
	ComboCtrl_Src->AddString(L"PNG");
	ComboCtrl_Src->AddString(L"GIF");
	ComboCtrl_Src->AddString(L"TIFF");
	ComboCtrl_Src->SetCurSel(0);

	CComboBox* ComboCtrl_Tgt = (CComboBox*)GetDlgItem(IDC_COMBO_TGTFORMAT);
	ComboCtrl_Tgt->AddString(L"JPG");
	ComboCtrl_Tgt->AddString(L"PNG");
	ComboCtrl_Tgt->AddString(L"BMP");
	ComboCtrl_Tgt->AddString(L"GIF");
	ComboCtrl_Tgt->AddString(L"TIFF");
	ComboCtrl_Tgt->SetCurSel(0);


	CScrollBar* pScrollBar1 = (CScrollBar*)GetDlgItem(IDC_SCROLLBAR_PER);
	pScrollBar1->SetScrollRange(0, 100, TRUE);
	//Ver1.0.0.2 pScrollBar1->SetScrollPos(80);
	pScrollBar1->SetScrollPos(76);	//Ver1.0.0.2
	pScrollBar1->EnableWindow(FALSE);

	((CEdit *)GetDlgItem(IDC_EDIT_PER))->EnableWindow(FALSE);
	((CStatic *)GetDlgItem(IDC_STATIC_LEVEL))->EnableWindow(FALSE);

	//Picture process
	if (m_Picture.Load(MAKEINTRESOURCE(IDR_GIF_PROGRESS),_T("GIF")))
		m_Picture.Draw();
	(CPictureEx*)GetDlgItem(IDC_STATIC_PROGRESS)->ShowWindow(SW_HIDE);

	((CButton *)GetDlgItem(IDC_RADIO_MULTI))->SetCheck(TRUE);

	return TRUE;  // return TRUE  unless you set the focus to a control
}

void CImgConverterDlg::OnSysCommand(UINT nID, LPARAM lParam)
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

void CImgConverterDlg::OnPaint()
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
HCURSOR CImgConverterDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}



void CImgConverterDlg::OnBnClickedButtonSrcbrowse()
{
	// TODO: Add your control notification handler code here
	TCHAR szSrcPath[MAX_PATH];
	BROWSEINFO bi;
	LPITEMIDLIST pItem;
	memset(&bi, 0, sizeof BROWSEINFO);

	((CButton *)GetDlgItem(IDC_RADIO_MULTI))->SetCheck(TRUE);
	((CButton *)GetDlgItem(IDC_RADIO_SINGLE))->SetCheck(FALSE);

	bi.hwndOwner = this->GetSafeHwnd();
	bi.pidlRoot = NULL;	
	bi.pszDisplayName = NULL;
	bi.lpszTitle = L"Please select the source directory.";
	bi.ulFlags = BIF_RETURNONLYFSDIRS|BIF_USENEWUI;
	bi.lpfn = NULL;
	bi.lParam = NULL;	
	bi.iImage = NULL;	

	pItem = SHBrowseForFolder(&bi);
	if (SHGetPathFromIDList(pItem,szSrcPath) == TRUE)
	{
		((CEdit *)GetDlgItem(IDC_EDIT_SOURCEFOLDER))->SetWindowTextW(szSrcPath);
		//AfxMessageBox(szSrcPath);
	}
}


void CImgConverterDlg::OnBnClickedButtonTgtbrowse()
{
	// TODO: Add your control notification handler code here
	TCHAR szTgtPath[MAX_PATH];
	BROWSEINFO bi;
	LPITEMIDLIST pItem;
	memset(&bi, 0, sizeof BROWSEINFO);

	((CButton *)GetDlgItem(IDC_RADIO_MULTI))->SetCheck(TRUE);
	((CButton *)GetDlgItem(IDC_RADIO_SINGLE))->SetCheck(FALSE);

	bi.hwndOwner = this->GetSafeHwnd();
	bi.pidlRoot = NULL;	
	bi.pszDisplayName = NULL;
	bi.lpszTitle = L"Please select the destination directory.";
	bi.ulFlags = BIF_RETURNONLYFSDIRS|BIF_USENEWUI;
	bi.lpfn = NULL;
	bi.lParam = NULL;	
	bi.iImage = NULL;	

	pItem = SHBrowseForFolder(&bi);
	if (SHGetPathFromIDList(pItem,szTgtPath) == TRUE)
	{
		((CEdit *)GetDlgItem(IDC_EDIT_TARGETFOLDER))->SetWindowTextW(szTgtPath);
		//AfxMessageBox(szSrcPath);
	}
}


void CImgConverterDlg::OnBnClickedButtonConvert()
{
	// TODO: Add your control notification handler code here
	CString str;
	CString strSrc;
	CString strTgt;
	CString strConvSrc;
	CString strConvTgt;
	CString strExtName;
	CString strSrcProc;
	CString strTgtProc;
	CString strFailItem;
	CString strDetDir;

	CString strScrFormat;

	CString strSingle;
	CString strSingleOut;

	int Src_index;
	int Tgt_index;
	int Count;
	int FailCount = 0;

	CStringArray strFileFullEx;
	CStringArray strFileTitleEx;

	UpdateData(TRUE);

	BOOL Mode = ((CButton *)GetDlgItem(IDC_RADIO_MULTI))->GetCheck();	

	if (Mode == TRUE)
	{
		CComboBox* ComboCtrl_Src = (CComboBox*)GetDlgItem(IDC_COMBO_SRCFORMAT);
		Src_index = ComboCtrl_Src->GetCurSel();

		CComboBox* ComboCtrl_Tgt = (CComboBox*)GetDlgItem(IDC_COMBO_TGTFORMAT);
		Tgt_index = ComboCtrl_Tgt->GetCurSel();

		CEdit *pEditSrc = (CEdit *)GetDlgItem(IDC_EDIT_SOURCEFOLDER);
		pEditSrc->GetWindowTextW(strSrc);

		CEdit *pEditTgt = (CEdit *)GetDlgItem(IDC_EDIT_TARGETFOLDER);
		pEditTgt->GetWindowTextW(strTgt);

		CFileFind FindEmpty;
		if (!(FindEmpty.FindFile(strSrc)))
		{
			AfxMessageBox(L"Source directory does not exist.\nPlease check and try again.");
			return;
		}

		if (strTgt.IsEmpty())
		{
			AfxMessageBox(L"Destination directory is empty.\nPlease check and try again.");
			return;
		}
		else if (!(FindEmpty.FindFile(strTgt)))
		{
			if (AfxMessageBox(L"Destination directory does not exist.\nDo you want to creat the directory?",
				MB_ICONINFORMATION | MB_OKCANCEL) == IDOK)
			{
				if (CreateDirectory(strTgt + L"\\", NULL) == NULL)

				{
					AfxMessageBox(L"Sorry, Create the specified directory failed.");
					return;
				}
			}
			else
				return;
		}


		if (Src_index == 0)
		{
			strConvSrc = strSrc + L"\\*.bmp";
			strScrFormat = L"*.bmp";
		}
		else if (Src_index == 1)
		{
			strConvSrc = strSrc + L"\\*.jpg";
			strScrFormat = L"*.jpg";
		}
		else if (Src_index == 2)
		{
			strConvSrc = strSrc + L"\\*.png";
			strScrFormat = L"*.png";
		}
		else if (Src_index == 3)
		{
			strConvSrc = strSrc + L"\\*.gif";
			strScrFormat = L"*.gif";
		}
		else if (Src_index == 4)
		{
			strConvSrc = strSrc + L"\\*.tif";
			strScrFormat = L"*.tif";
		}

		GetSpecifiedFiles(strConvSrc, strFileFullEx, strFileTitleEx);

		Count = strFileFullEx.GetSize();
		if (Count == 0)
		{
			str.Format(L"Can not found \"%s\" file in\n%s,\nPlease check or select right format.",
				strScrFormat, strSrc);
			AfxMessageBox(str, MB_ICONINFORMATION);
			return;
		}

		(CPictureEx*)GetDlgItem(IDC_STATIC_PROGRESS)->ShowWindow(SW_SHOW);
		UpdateWindow();
	}
	else
	{
		CComboBox* ComboCtrl_Tgt = (CComboBox*)GetDlgItem(IDC_COMBO_TGTFORMAT);
		Tgt_index = ComboCtrl_Tgt->GetCurSel();

		CEdit *pEditSrc = (CEdit *)GetDlgItem(IDC_EDIT_SINGLE);
		pEditSrc->GetWindowTextW(strSingle);

		if (strSingle.IsEmpty())
		{
			AfxMessageBox(L"The File does not exist.\nPlease check and try again.");
			return;
		}
		int i = strSingle.ReverseFind('.'); 
		strSingleOut = strSingle.Left(i);
		//AfxMessageBox(strSingleOut);
	}

	// Initialize GDI+.
	GdiplusStartupInput gdiplusStartupInput;
	ULONG_PTR gdiplusToken;
	GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, NULL);

	CLSID             encoderClsid;
	EncoderParameters encoderParameters;
	ULONG             quality;
	Status            stat;

	//Image*   image = new Image(L"Test.bmp");

	// Get the CLSID of encoder.
	if (Tgt_index == 0)
	{
		GetEncoderClsid(L"image/jpeg", &encoderClsid);
		strExtName = L".jpg";

		if (((CButton*)GetDlgItem(IDC_CHECK_ADV))->GetCheck())
		{
			encoderParameters.Count = 1;
			encoderParameters.Parameter[0].Guid = EncoderQuality;
			encoderParameters.Parameter[0].Type = EncoderParameterValueTypeLong;
			encoderParameters.Parameter[0].NumberOfValues = 1;

			// Save the image as a JPEG with quality level 0~100.
			quality = m_PerValue;	//Set the quality value by end user
			encoderParameters.Parameter[0].Value = &quality;
			//stat = image->Save(L"Shapes001.jpg", &encoderClsid, &encoderParameters);
		}
	}
	else if (Tgt_index == 1)
	{
		GetEncoderClsid(L"image/png", &encoderClsid);
		strExtName = L".png";
	}
	else if (Tgt_index == 2)
	{
		GetEncoderClsid(L"image/bmp", &encoderClsid);
		strExtName = L".bmp";
	}
	else if (Tgt_index == 3)
	{
		GetEncoderClsid(L"image/gif", &encoderClsid);
		strExtName = L".gif";
	}
	else if (Tgt_index == 4)
	{
		GetEncoderClsid(L"image/tiff", &encoderClsid);
		strExtName = L".tif";
	}

	if (Mode == TRUE)
	{
		for (int i = 0; i < Count; i++)
		{
			strSrcProc = strSrc + L"\\" + strFileFullEx.GetAt(i);
			strTgtProc = strTgt + L"\\" + strFileTitleEx.GetAt(i) + strExtName;
			Image* image = new Image(strSrcProc);
			if ((Tgt_index == 0) && ((CButton*)GetDlgItem(IDC_CHECK_ADV))->GetCheck())
			{
				stat = image->Save(strTgtProc, &encoderClsid, &encoderParameters);
			}
			else
			{
				stat = image->Save(strTgtProc, &encoderClsid, NULL);
			}

			if(stat == Ok)
				;//AfxMessageBox(L"Test.png was saved successfully\n");
			else
			{
				//str.Format(L"Failure: stat = %d\n", stat);
				//AfxMessageBox(str);
				FailCount ++;
				strFailItem += strSrcProc + L"\n";
			}
			delete image;
		}
		GdiplusShutdown(gdiplusToken);

		if (FailCount != 0)
			AfxMessageBox(L"Sorry, the following items conversion failed:\n\n" + strFailItem);
		else
			AfxMessageBox(L"Conversion successfully.\nPlease check the Destination directory.", MB_ICONINFORMATION);

		(CPictureEx*)GetDlgItem(IDC_STATIC_PROGRESS)->ShowWindow(SW_HIDE);
	}

	else
	{
		strSingleOut += strExtName;
		Image* image = new Image(strSingle);
		if ((Tgt_index == 0) && ((CButton*)GetDlgItem(IDC_CHECK_ADV))->GetCheck())
		{
			stat = image->Save(strSingleOut, &encoderClsid, &encoderParameters);
		}
		else
		{
			stat = image->Save(strSingleOut, &encoderClsid, NULL);
		}

		if(stat != Ok)
			AfxMessageBox(L"Sorry, conversion failed.");
		else
		{
			str.Format(L"Conversion successfully, please check. Location:\n\n %s",strSingleOut);
			AfxMessageBox(str, MB_ICONINFORMATION);
		}
		
		delete image;
		GdiplusShutdown(gdiplusToken);
	}

}


int CImgConverterDlg::GetEncoderClsid(const WCHAR* format, CLSID* pClsid)
{
	UINT  num = 0;          // number of image encoders
	UINT  size = 0;         // size of the image encoder array in bytes

	ImageCodecInfo* pImageCodecInfo = NULL;

	GetImageEncodersSize(&num, &size);
	if(size == 0)
		return -1;  // Failure

	pImageCodecInfo = (ImageCodecInfo*)(malloc(size));
	if(pImageCodecInfo == NULL)
		return -1;  // Failure

	GetImageEncoders(num, size, pImageCodecInfo);

	for(UINT j = 0; j < num; ++j)
	{
		if( wcscmp(pImageCodecInfo[j].MimeType, format) == 0 )
		{
			*pClsid = pImageCodecInfo[j].Clsid;
			free(pImageCodecInfo);
			return j;  // Success
		}    
	}

	free(pImageCodecInfo);
	return -1;  // Failure
}


void CImgConverterDlg::GetSpecifiedFiles(CString strPath, CStringArray &strFileFull, CStringArray &strFileTitle) //Search a directory for a specified file name
{
	CFileFind finder;
	strFileFull.RemoveAll();					//Removes all the elements from this array
	strFileTitle.RemoveAll();
	CString strFileName;
	CString strTitleName;

	BOOL bWorking = finder.FindFile(strPath);	//Perform file search
	while(bWorking)
	{
		bWorking = finder.FindNextFile();		//Search next file

		if(finder.IsDirectory())				//If it is a directory, end this search
			continue;

		strFileName = finder.GetFileName();		//Get the file name
		strTitleName = finder.GetFileTitle();	//Get the title of file

		strFileFull.Add(strFileName);			//Adds an element to array
		strFileTitle.Add(strTitleName);			//Adds the elements
	}
	finder.Close();								//Closes the search request
} 


void CImgConverterDlg::OnCbnSelchangeComboTgtformat()
{
	// TODO: Add your control notification handler code here
	CComboBox* ComboCtrl = (CComboBox*)GetDlgItem(IDC_COMBO_TGTFORMAT);
	int index = ComboCtrl->GetCurSel();
	if (index == 0)
	{
		((CButton*)GetDlgItem(IDC_CHECK_ADV))->EnableWindow(TRUE);
		if (((CButton*)GetDlgItem(IDC_CHECK_ADV))->GetCheck())
		{
			((CScrollBar *)GetDlgItem(IDC_SCROLLBAR_PER))->EnableWindow(TRUE);
			((CEdit *)GetDlgItem(IDC_EDIT_PER))->EnableWindow(TRUE);
			((CStatic *)GetDlgItem(IDC_STATIC_LEVEL))->EnableWindow(TRUE);
		}
	}
	else
	{
		((CButton*)GetDlgItem(IDC_CHECK_ADV))->EnableWindow(FALSE);
		((CScrollBar *)GetDlgItem(IDC_SCROLLBAR_PER))->EnableWindow(FALSE);
		((CEdit *)GetDlgItem(IDC_EDIT_PER))->EnableWindow(FALSE);
		((CStatic *)GetDlgItem(IDC_STATIC_LEVEL))->EnableWindow(FALSE);
	}
}


void CImgConverterDlg::OnBnClickedCheckAdv()
{
	// TODO: Add your control notification handler code here
	if (((CButton*)GetDlgItem(IDC_CHECK_ADV))->GetCheck())
	{
		((CScrollBar *)GetDlgItem(IDC_SCROLLBAR_PER))->EnableWindow(TRUE);
		((CEdit *)GetDlgItem(IDC_EDIT_PER))->EnableWindow(TRUE);
		((CStatic *)GetDlgItem(IDC_STATIC_LEVEL))->EnableWindow(TRUE);
	}
	else
	{
		((CScrollBar *)GetDlgItem(IDC_SCROLLBAR_PER))->EnableWindow(FALSE);
		((CEdit *)GetDlgItem(IDC_EDIT_PER))->EnableWindow(FALSE);
		((CStatic *)GetDlgItem(IDC_STATIC_LEVEL))->EnableWindow(FALSE);
	}
}


void CImgConverterDlg::OnHScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar)
{
	// TODO: Add your message handler code here and/or call default
	int minpos;
	int maxpos;
	//GetScrollRange(SB_HORZ, &minpos, &maxpos); 
	pScrollBar->GetScrollRange(&minpos, &maxpos);

	//maxpos = GetScrollLimit(SB_HORZ);

	// Get the current position of scroll box.
	//int curpos = GetScrollPos(SB_HORZ);
	int curpos = pScrollBar->GetScrollPos();

	// Determine the new position of scroll box.
	switch (nSBCode)
	{
	case SB_LEFT:      // Scroll to far left.
		curpos = minpos;
		break;

	case SB_RIGHT:      // Scroll to far right.
		curpos = maxpos;
		break;

	case SB_ENDSCROLL:   // End scroll.
		break;

	case SB_LINELEFT:      // Scroll left.
		if (curpos > minpos)
			curpos--;
		break;

	case SB_LINERIGHT:   // Scroll right.
		if (curpos < maxpos)
			curpos++;
		break;

	case SB_PAGELEFT:    // Scroll one page left.
		{
			// Get the page size. 
			/*SCROLLINFO   info;
			GetScrollInfo(SB_HORZ, &info, SIF_ALL);

			if (curpos > minpos)
			curpos = max(minpos, curpos - (int) info.nPage);*/
			curpos -= 10;
		}
		break;

	case SB_PAGERIGHT:      // Scroll one page right.
		{
			// Get the page size. 
			/*SCROLLINFO   info;
			GetScrollInfo(SB_HORZ, &info, SIF_ALL);

			if (curpos < maxpos)
			curpos = min(maxpos, curpos + (int) info.nPage);*/
			curpos += 10;
		}
		break;

	case SB_THUMBPOSITION: // Scroll to absolute position. nPos is the position
		curpos = nPos;      // of the scroll box at the end of the drag operation.
		break;

	case SB_THUMBTRACK:   // Drag scroll box to specified position. nPos is the
		curpos = nPos;     // position that the scroll box has been dragged to.
		break;
	}

	// Set the new position of the thumb (scroll box).
	//SetScrollPos(SB_HORZ, curpos);
	pScrollBar->SetScrollPos(curpos);

	m_PerValue = curpos;
	UpdateData(FALSE);

	CDialogEx::OnHScroll(nSBCode, nPos, pScrollBar);
}


void CImgConverterDlg::OnEnChangeEditPer()
{
	// TODO:  If this is a RICHEDIT control, the control will not
	// send this notification unless you override the CDialogEx::OnInitDialog()
	// function and call CRichEditCtrl().SetEventMask()
	// with the ENM_CHANGE flag ORed into the mask.

	// TODO:  Add your control notification handler code here
	UpdateData(TRUE);

	CScrollBar* pScrollBar = (CScrollBar*)GetDlgItem(IDC_SCROLLBAR_PER);
	pScrollBar->SetScrollPos(m_PerValue);

	UpdateData(FALSE);
}


HBRUSH CImgConverterDlg::OnCtlColor(CDC* pDC, CWnd* pWnd, UINT nCtlColor)
{
	HBRUSH hbr = CDialogEx::OnCtlColor(pDC, pWnd, nCtlColor);

	// TODO:  Change any attributes of the DC here
	/*if(nCtlColor == CTLCOLOR_STATIC)
	{
		HBRUSH hbr = CreateSolidBrush(RGB(185, 255, 185));
		pDC->SetTextColor(RGB(0, 0, 255));
		pDC->SetBkColor(RGB(255, 255, 255));
		return hbr;
	}
	if(nCtlColor==CTLCOLOR_DLG)
	{
		HBRUSH hbr=CreateSolidBrush(RGB(255, 255, 255));
		return hbr;
	}*/
	if (pWnd->GetDlgCtrlID() == IDC_STATIC_TITLE)
	{
		//HBRUSH hbr = CreateSolidBrush(RGB(255, 255, 2555));
		pDC->SetTextColor(RGB(0, 102, 129));
		pDC-> SetBkMode(TRANSPARENT);
		return hbr;
	}
	// TODO:  Return a different brush if the default is not desired
	return hbr;
}


void CImgConverterDlg::OnBnClickedButtonAbout()
{
	// TODO: Add your control notification handler code here
	CAboutDlg dlgAbout;
	dlgAbout.DoModal();
}


BOOL CImgConverterDlg::PreTranslateMessage(MSG* pMsg)
{
	if (WM_KEYDOWN == pMsg->message)
	{
		UINT nKey = (int) pMsg->wParam;
		if(/*VK_RETURN == nKey || */VK_ESCAPE == nKey)
			return TRUE;
	}
	else if (WM_LBUTTONDOWN == pMsg->message)
	{
		POINT MousePoint;
		GetCursorPos(&MousePoint);
		HANDLE hwnd = WindowFromPoint(MousePoint);
		if (hwnd == GetDlgItem(IDC_STATIC_USER))
		{
			OnBnClickedButtonAbout();
		}
	}

	return CDialog::PreTranslateMessage(pMsg);
}


LRESULT CImgConverterDlg::OnNcHitTest(CPoint point)
{
	// TODO: Add your message handler code here and/or call default
	CRect rc;
	GetClientRect(&rc);
	ClientToScreen(&rc);
	return rc.PtInRect(point) ? HTCAPTION : CDialog::OnNcHitTest(point);
	return CDialogEx::OnNcHitTest(point);
}


void CImgConverterDlg::OnBnClickedButtonSingle()
{
	// TODO: Add your control notification handler code here
	((CButton *)GetDlgItem(IDC_RADIO_SINGLE))->SetCheck(TRUE);
	((CButton *)GetDlgItem(IDC_RADIO_MULTI))->SetCheck(FALSE);

	WCHAR szFilters[] = L"All Files (*.*)|*.*||";
	CFileDialog fileDlg(TRUE, L"All Files", L"*.*", OFN_FILEMUSTEXIST, szFilters, this);
	if (fileDlg.DoModal() == IDOK)
	{
		//m_strFile = fileDlg.GetPathName();
		//UpdateData(FALSE);
		((CEdit *)GetDlgItem(IDC_EDIT_SINGLE))->SetWindowTextW(fileDlg.GetPathName());
	}
}

//Ver1.0.0.3{
void CImgConverterDlg::OnBnClickedCheckTop()
{
	// TODO: Add your control notification handler code here
	if (((CButton*)GetDlgItem(IDC_CHECK_TOP))->GetCheck())
	{
		SetWindowPos(&CWnd::wndTopMost, 0, 0, 0, 0, SWP_FRAMECHANGED|SWP_NOSIZE|SWP_NOMOVE);
	}
	else
	{
		SetWindowPos(&CWnd::wndNoTopMost, 0, 0, 0, 0, SWP_FRAMECHANGED|SWP_NOSIZE|SWP_NOMOVE);
	}
}
//Ver1.0.0.3
