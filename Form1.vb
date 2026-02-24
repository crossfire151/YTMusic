Imports System.ComponentModel
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports System.Windows.Forms.VisualStyles.VisualStyleElement
Imports DiscordRPC
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms
Imports Newtonsoft
Imports Newtonsoft.Json

Public Class Form1
    Private WithEvents YTPlayer As WebView2
    Private rpc As New DiscordRpcClient(My.Settings.DiscordAppId) ' Your Rich Presence App ID
    Private currentSong As SongInfo
    Private listenAlongEnabled As Boolean = False
    Private trayIcon As New NotifyIcon()
    Private trayMenu As New ContextMenuStrip()


    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Debug.WriteLine($"Form handle at load: {Me.Handle}")
        ' Initialize RPC
        rpc.Initialize()
        AddHandler rpcTimer.Tick, AddressOf RpcTimer_Tick
        rpcTimer.Interval = 1000
        rpcTimer.Start()

        ' Initialize WebView2
        YTPlayer = New WebView2 With {.Dock = DockStyle.Fill}
        Me.Controls.Add(YTPlayer)
        Dim userDataFolder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YTMusic")
        Dim env = Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder)
        Await YTPlayer.EnsureCoreWebView2Async(env)
        YTPlayer.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = False


        YTPlayer.CoreWebView2.Settings.IsStatusBarEnabled = False
        YTPlayer.CoreWebView2.Settings.AreDevToolsEnabled = True

        ' Event Handlers
        AddHandler YTPlayer.CoreWebView2.DocumentTitleChanged, AddressOf TitleChangedHandler
        AddHandler YTPlayer.CoreWebView2.FaviconChanged, AddressOf OnFaviconChanged


        ' Navigate to YT Music
        YTPlayer.CoreWebView2.Navigate("https://music.youtube.com/")

        ' Restore window size
        If My.Settings.SizeX > 0 AndAlso My.Settings.SizeY > 0 Then
            Me.Size = New Size(My.Settings.SizeX, My.Settings.SizeY)
            Me.CenterToScreen()
        End If
        AddHandler YTPlayer.KeyDown, AddressOf WebViewKeyDown

        ' System Tray Setup
        trayMenu.Items.Add("Open", Nothing, Sub() ShowFromTray())
        trayMenu.Items.Add("Settings", Nothing, Sub()
                                                    Dim settings As New AppSettings()
                                                    settings.ShowDialog()
                                                End Sub)
        trayMenu.Items.Add("-")
        trayMenu.Items.Add("Quit", Nothing, Sub() Application.Exit())

        trayIcon.Icon = Me.Icon
        trayIcon.Text = "YT Music"
        trayIcon.ContextMenuStrip = trayMenu
        trayIcon.Visible = True

        AddHandler trayIcon.DoubleClick, Sub() ShowFromTray()
    End Sub


    Private Sub ShowFromTray()
        Me.Show()
        Me.WindowState = FormWindowState.Normal
        Me.BringToFront()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            Me.Hide()
        Else
            trayIcon.Visible = False
            rpc.Deinitialize()
            MyBase.OnFormClosing(e)
        End If
    End Sub

    Private Sub WebViewKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.MediaPlayPause OrElse
       e.KeyCode = Keys.MediaNextTrack OrElse
       e.KeyCode = Keys.MediaPreviousTrack Then
            Dim cmd As Integer = 0
            If e.KeyCode = Keys.MediaPlayPause Then cmd = 14
            If e.KeyCode = Keys.MediaNextTrack Then cmd = 11
            If e.KeyCode = Keys.MediaPreviousTrack Then cmd = 12
            Select Case cmd
                Case 14 : MediaPlayPause()
                Case 11 : MediaNext()
                Case 12 : MediaPrevious()
            End Select
            e.Handled = True
        End If
    End Sub

    ' ----------------------------
    ' --- Now Playing Logic -----
    ' ----------------------------
    Private Async Sub TitleChangedHandler(sender As Object, e As Object)
        Await UpdateNowPlaying()
    End Sub

    Private Async Function GetNowPlayingAsync() As Task(Of SongInfo)
        Dim script As String =
"(() => {
    try {
        let titleEl = document.querySelector('ytmusic-player-bar .title');
        let artistEls = document.querySelectorAll('ytmusic-player-bar .byline a[href*=""channel""]');
        let title = titleEl ? titleEl.textContent.trim() : '';
        let artists = [];
        artistEls.forEach(a => artists.push(a.textContent.trim()));
        let videoId = '';
        let position = 0;
        let duration = 0;
        let thumbnail = '';
        let player = document.querySelector('#movie_player');
let isPlaying = player ? player.getPlayerState() === 1 : false;
        if (player && player.getVideoData) {
            videoId = player.getVideoData().video_id || '';
            position = player.getCurrentTime ? player.getCurrentTime() : 0;
            duration = player.getDuration ? player.getDuration() : 0;
            if (videoId) thumbnail = 'https://img.youtube.com/vi/' + videoId + '/hqdefault.jpg';
        }
        return JSON.stringify({ title: title, artists: artists, videoId: videoId, position: position, duration: duration, thumbnail: thumbnail, isPlaying: isPlaying });
    } catch(e) {
        return JSON.stringify({ title: '', artists: [], videoId: '', position: 0, duration: 0, thumbnail: '' });
    }
})()"

        Dim raw As String = Await YTPlayer.CoreWebView2.ExecuteScriptAsync(script)
        If String.IsNullOrWhiteSpace(raw) OrElse raw = "null" Then Return Nothing

        Dim cleanedJson As String
        Try
            cleanedJson = JsonConvert.DeserializeObject(Of String)(raw)
        Catch
            cleanedJson = raw.Trim(""""c)
        End Try
        If String.IsNullOrWhiteSpace(cleanedJson) Then Return Nothing

        Return JsonConvert.DeserializeObject(Of SongInfo)(cleanedJson)
    End Function

    Private Async Function UpdateNowPlaying() As Task
        Dim data = Await GetNowPlayingAsync()
        If data Is Nothing Then Return

        currentSong = data

        Dim artistText As String = If(data.artists IsNot Nothing, String.Join(", ", data.artists), "")
        If Not String.IsNullOrEmpty(data.title) AndAlso artistText.Contains(data.title) Then
            artistText = artistText.Replace(data.title, "").Trim().Trim(","c, " "c)
        End If


        ' Update UI
        If Me.InvokeRequired Then
            Me.Invoke(Sub()
                          SongName.Text = data.title
                          SongArtist.Text = artistText
                      End Sub)
        Else
            SongName.Text = data.title
            SongArtist.Text = artistText
        End If

        ' Update RPC with Listen Along button
        UpdateRpc()
    End Function



    ' ----------------------------
    ' --- Discord RPC -----------
    ' ----------------------------
    Private Sub UpdateRpc()
        If currentSong Is Nothing Then Return

        If Not My.Settings.RpcEnabled Then
            rpc.ClearPresence()
            Return
        End If

        Dim artistText As String = If(currentSong.artists IsNot Nothing, String.Join(", ", currentSong.artists), "")
        If Not String.IsNullOrEmpty(currentSong.title) AndAlso artistText.Contains(currentSong.title) Then
            artistText = artistText.Replace(currentSong.title, "").Trim().Trim(","c, " "c)
        End If

        Dim listenAlongUrl As String = ""
        If My.Settings.ListenAlongEnabled AndAlso Not String.IsNullOrEmpty(currentSong.videoId) Then
            listenAlongUrl = $"https://ytmusic.crossfire151.co.uk/?video={currentSong.videoId}&t={CInt(Math.Floor(currentSong.position))}"
        End If

        Dim startTime As DateTimeOffset = DateTimeOffset.UtcNow.AddSeconds(-currentSong.position)
        Dim endTime As DateTimeOffset = startTime.AddSeconds(currentSong.duration)

        Dim stateText As String = If(String.IsNullOrEmpty(artistText), "", artistText)
        If Not currentSong.isPlaying Then stateText = stateText & " | ⏸ Paused"

        Dim presence As New RichPresence() With {
        .Details = currentSong.title,
        .State = stateText,
        .Assets = New Assets() With {
            .LargeImageKey = currentSong.thumbnail,
            .LargeImageText = currentSong.title
        },
        .Timestamps = If(currentSong.isPlaying,
            New Timestamps() With {
                .Start = startTime.UtcDateTime,
                .End = endTime.UtcDateTime
            },
            Nothing)
    }

        If Not String.IsNullOrEmpty(listenAlongUrl) Then
            presence.Buttons = New DiscordRPC.Button() {
            New DiscordRPC.Button() With {
                .Label = "Listen Along",
                .Url = listenAlongUrl
            }
        }
        End If

        rpc.SetPresence(presence)
    End Sub

    Private Async Sub RpcTimer_Tick(sender As Object, e As EventArgs) Handles rpcTimer.Tick
        Await UpdateNowPlaying()
    End Sub

    ' ----------------------------
    ' --- Favicon & Title -------
    ' ----------------------------
    Private Async Sub OnFaviconChanged(sender As Object, e As Object)
        Try
            Dim stream = Await YTPlayer.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png)
            If stream IsNot Nothing Then
                Using ms As New MemoryStream()
                    Await stream.CopyToAsync(ms)
                    ms.Position = 0
                    Using bmp As New Bitmap(ms)
                        Dim ico = Icon.FromHandle(bmp.GetHicon())
                        Me.Icon = CType(ico.Clone(), Icon)
                        DestroyIcon(ico.Handle)
                    End Using
                End Using
            End If
        Catch
        End Try
    End Sub


    ' ----------------------------
    ' ---  Media Controls  -------
    ' ----------------------------
    Protected Overrides Sub WndProc(ByRef m As Message)
        Const WM_APPCOMMAND As Integer = &H319
        If m.Msg = WM_APPCOMMAND Then
            Dim cmd As Integer = (CInt(m.LParam) >> 16) And &HFFFF
            Select Case cmd
                Case 14 : MediaPlayPause()
                Case 11 : MediaNext()
                Case 12 : MediaPrevious()
            End Select
            m.Result = CType(1, IntPtr)
            Return
        End If
        MyBase.WndProc(m)
    End Sub

    Private Sub MediaPlayPause()
        Me.Invoke(Sub()
                      YTPlayer.CoreWebView2.ExecuteScriptAsync(
            "Array.from(document.querySelectorAll('button')).find(b => b.getAttribute('aria-label') === 'Play' || b.getAttribute('aria-label') === 'Pause')?.click();")
                  End Sub)
    End Sub

    Private Sub MediaNext()
        Me.Invoke(Sub()
                      YTPlayer.CoreWebView2.ExecuteScriptAsync(
            "Array.from(document.querySelectorAll('button')).find(b => b.getAttribute('aria-label') === 'Next')?.click();")
                  End Sub)
    End Sub

    Private Sub MediaPrevious()
        Me.Invoke(Sub()
                      YTPlayer.CoreWebView2.ExecuteScriptAsync(
            "Array.from(document.querySelectorAll('button')).find(b => b.getAttribute('aria-label') === 'Previous')?.click();")
                  End Sub)
    End Sub


    ' ----------------------------
    ' ---       Title      -------
    ' ----------------------------

    Private Sub OnTitleChanged(sender As Object, e As EventArgs)
        If YTPlayer Is Nothing OrElse YTPlayer.CoreWebView2 Is Nothing Then Return
        Dim pageTitle As String = YTPlayer.CoreWebView2.DocumentTitle
        If pageTitle.Contains(" - YouTube Music") Then pageTitle = pageTitle.Replace(" - YouTube Music", "")
        Me.Text = pageTitle
    End Sub

    <Runtime.InteropServices.DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function DestroyIcon(hIcon As IntPtr) As Boolean
    End Function

    Private Sub Form1_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        My.Settings.SizeX = Me.Size.Width
        My.Settings.SizeY = Me.Size.Height
        My.Settings.Save()
    End Sub

    ' ----------------------------
    ' --- Listen Along Handler --
    ' ----------------------------
    Public Async Sub ListenAlong(videoId As String, Optional t As Integer = 0)
        If String.IsNullOrEmpty(videoId) Then Return
        listenAlongEnabled = True
        YTPlayer.CoreWebView2.Navigate($"https://music.youtube.com/watch?v={videoId}")
        Await Task.Delay(2000)
        If t > 0 Then
            Await YTPlayer.CoreWebView2.ExecuteScriptAsync($"document.querySelector('#movie_player').seekTo({t}, true);")
        End If
        Await UpdateNowPlaying()
    End Sub


    Private Sub Form1_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        trayIcon.ShowBalloonTip(3000, "YT Music", "YT Music is still running. Double-click the tray icon to reopen.", ToolTipIcon.Info)
    End Sub
End Class

Public Class SongInfo
    Public Property isPlaying As Boolean = False
    Public Property title As String = ""
    Public Property artists As List(Of String) = New List(Of String)()
    Public Property position As Double = 0
    Public Property duration As Double = 0
    Public Property videoId As String = ""
    Public Property thumbnail As String = ""
End Class