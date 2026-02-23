Imports System.IO
Imports System.Runtime.Remoting.Metadata.W3cXsd2001
Imports DiscordRPC
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms
Imports Newtonsoft.Json
Public Class Form1
    Private WithEvents YTPlayer As WebView2
    Dim rpc As New DiscordRpcClient("1475533199420686356")
    Dim now = DateTimeOffset.UtcNow

    Private WithEvents rpcTimer As New System.Windows.Forms.Timer()
    Private currentSong As SongInfo
    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        rpc.Initialize()
        YTPlayer = New WebView2()
        YTPlayer.Dock = DockStyle.Fill
        Me.Controls.Add(YTPlayer)

        Await YTPlayer.EnsureCoreWebView2Async()

        YTPlayer.CoreWebView2.Settings.IsStatusBarEnabled = False
        YTPlayer.CoreWebView2.Settings.AreDevToolsEnabled = True
        AddHandler YTPlayer.CoreWebView2.FaviconChanged, AddressOf OnFaviconChanged
        AddHandler YTPlayer.CoreWebView2.DocumentTitleChanged, AddressOf OnTitleChanged

        YTPlayer.CoreWebView2.Navigate("https://music.youtube.com/")

        AddHandler YTPlayer.CoreWebView2.DocumentTitleChanged, AddressOf TitleChangedHandler
        If Not My.Settings.SizeX = "" Or Not My.Settings.SizeY = "" Then
            Me.Size = New Size(My.Settings.SizeX, My.Settings.SizeY)
            Me.CenterToScreen()
        End If
    End Sub

    Private Async Sub TitleChangedHandler(sender As Object, args As Object)
        Await UpdateNowPlaying()
    End Sub

    Private Async Function GetNowPlayingAsync() As Task(Of SongInfo)

        Dim script As String =
    "
    (function() {
        let titleEl = document.querySelector('ytmusic-player-bar .title');
        let artistEls = document.querySelectorAll('ytmusic-player-bar .byline a');

        let title = titleEl ? titleEl.textContent.trim() : '';

        let artists = [];
        artistEls.forEach(a => {
            artists.push(a.textContent.trim());
        });

        return JSON.stringify({
            title: title,
            artists: artists
        });
    })();
    "

        Dim raw = Await YTPlayer.CoreWebView2.ExecuteScriptAsync(script)

        If String.IsNullOrWhiteSpace(raw) Then Return Nothing

        ' Properly unescape WebView2 JSON wrapper
        Dim cleanedJson As String = JsonConvert.DeserializeObject(Of String)(raw)

        Return JsonConvert.DeserializeObject(Of SongInfo)(cleanedJson)

    End Function

    Private Async Function UpdateNowPlaying() As Task
        Dim data = Await GetNowPlayingAsync()
        If data Is Nothing Then Return

        currentSong = data ' Store globally for the timer

        Dim artistText As String = If(data.artists IsNot Nothing, String.Join(", ", data.artists), "")

        If Not String.IsNullOrEmpty(data.title) AndAlso artistText.Contains(data.title) Then
            artistText = artistText.Replace(data.title, "").Trim()
            artistText = artistText.Trim(","c, " "c)
        End If

        If Me.InvokeRequired Then
            Me.Invoke(Sub()
                          SongName.Text = If(data.title, "")
                          SongArtist.Text = artistText
                      End Sub)
        Else
            SongName.Text = If(data.title, "")
            SongArtist.Text = artistText
        End If

        ' Update RPC immediately
        UpdateRpc()
    End Function

    Private Sub UpdateRpc()
        If currentSong Is Nothing Then Return

        ' Increment position by 1 second if already playing
        If currentSong.position.HasValue Then
            currentSong.position += 1
        End If

        Dim currentPosSec As Double = If(currentSong.position, 0)
        Dim totalDurSec As Double = If(currentSong.duration, 0)

        Dim currentPosStr As String = String.Format("{0}:{1:00}", CInt(currentPosSec \ 60), CInt(currentPosSec Mod 60))
        Dim totalDurStr As String = If(totalDurSec > 0, String.Format("{0}:{1:00}", CInt(totalDurSec \ 60), CInt(totalDurSec Mod 60)), "0:00")
        Dim playbackDisplay As String = $"{currentPosStr} / {totalDurStr}"
        playbackDisplay = ""

        Dim artistText As String = If(currentSong.artists IsNot Nothing, String.Join(", ", currentSong.artists), "")
        If Not String.IsNullOrEmpty(currentSong.title) AndAlso artistText.Contains(currentSong.title) Then
            artistText = artistText.Replace(currentSong.title, "").Trim().Trim(","c, " "c)
        End If

        Dim presence As New RichPresence() With {
        .Details = currentSong.title,
        .State = If(String.IsNullOrEmpty(artistText), playbackDisplay, $"{artistText}"),
        .Assets = New Assets() With {
            .LargeImageKey = "youtube_music_logo",
            .LargeImageText = "Listening on YouTube Music"
        }
    }

        ' Timestamps for progress bar
        Dim ts As New Timestamps()
        ts.Start = DateTime.UtcNow.AddSeconds(-currentPosSec)
        If totalDurSec > 0 Then
            ts.[End] = DateTime.UtcNow.AddSeconds(totalDurSec - currentPosSec)
        End If
        presence.Timestamps = ts

        rpc.SetPresence(presence)
    End Sub

    Private Sub rpcTimer_Tick(sender As Object, e As EventArgs) Handles rpcTimer.Tick
        UpdateRpc()
    End Sub

    Private Async Sub OnFaviconChanged(sender As Object, e As Object)

        Try
            Dim stream = Await YTPlayer.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png)

            If stream IsNot Nothing Then
                Using ms As New MemoryStream()
                    Await stream.CopyToAsync(ms)
                    ms.Position = 0

                    Using bmp As New Bitmap(ms)
                        Dim iconHandle = bmp.GetHicon()
                        Dim ico = Icon.FromHandle(iconHandle)

                        Me.Icon = CType(ico.Clone(), Icon)

                        DestroyIcon(iconHandle)
                    End Using
                End Using
            End If

        Catch
            ' Ignore errors quietly
        End Try

    End Sub

    Private Sub OnTitleChanged(sender As Object, e As Object)

        If YTPlayer Is Nothing OrElse YTPlayer.CoreWebView2 Is Nothing Then Return

        Dim pageTitle As String = YTPlayer.CoreWebView2.DocumentTitle

        ' Optional cleanup for YouTube Music
        If pageTitle.Contains(" - YouTube Music") Then
            pageTitle = pageTitle.Replace(" - YouTube Music", "")
        End If

        ' Update form title safely
        If Me.InvokeRequired Then
            Me.Invoke(Sub() Me.Text = pageTitle)
        Else
            Me.Text = pageTitle
        End If

    End Sub

    <Runtime.InteropServices.DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function DestroyIcon(hIcon As IntPtr) As Boolean
    End Function

    Private Sub Form1_SizeChanged(sender As Object, e As EventArgs) Handles Me.SizeChanged
        My.Settings.SizeX = Me.Size.Width
        My.Settings.SizeY = Me.Size.Height
        My.Settings.Save()
    End Sub
End Class

Public Class SongInfo
    Public Property title As String = ""                  ' Default to empty string
    Public Property artists As List(Of String) = New List(Of String)() ' Always initialized
    Public Property position As Double? = 0              ' Current playback in seconds
    Public Property duration As Double? = 0              ' Total duration in seconds

    ' Helper: Returns position as mm:ss safely
    Public ReadOnly Property PositionText As String
        Get
            Dim pos As Double = If(position, 0)
            Return String.Format("{0}:{1:00}", CInt(pos \ 60), CInt(pos Mod 60))
        End Get
    End Property

    ' Helper: Returns duration as mm:ss safely
    Public ReadOnly Property DurationText As String
        Get
            Dim dur As Double = If(duration, 0)
            Return String.Format("{0}:{1:00}", CInt(dur \ 60), CInt(dur Mod 60))
        End Get
    End Property
End Class
