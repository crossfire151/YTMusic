Imports DiscordRPC

Public NotInheritable Class Splash

    Private Sub Splash_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        If My.Application.Info.Title <> "" Then
            ApplicationTitle.Text = My.Application.Info.Title
        Else
            ApplicationTitle.Text = System.IO.Path.GetFileNameWithoutExtension(My.Application.Info.AssemblyName)
        End If

        Version.Text = System.String.Format(Version.Text, My.Application.Info.Version.Major, My.Application.Info.Version.Minor)
        Copyright.Text = My.Application.Info.Copyright

        If Not ValidateAppId() Then
            MessageBox.Show("This version of YT Music is no longer valid. Please download the latest version from GitHub.", "Invalid Application", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Application.Exit()
            Return
        End If

        Timer1.Interval = 50
        Timer1.Start()
    End Sub

    Private Function ValidateAppId() As Boolean
        Return My.Settings.DiscordAppId = "1475597678342574110"
    End Function

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        ProgressBar1.Increment(1)
        If ProgressBar1.Value >= ProgressBar1.Maximum Then
            Timer1.Stop()
            Form1.Show()
            Me.Close()
        End If
    End Sub

End Class