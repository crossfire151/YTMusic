<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.SongName = New System.Windows.Forms.Label()
        Me.SongArtist = New System.Windows.Forms.Label()
        Me.SongTime = New System.Windows.Forms.Label()
        Me.SuspendLayout()
        '
        'SongName
        '
        Me.SongName.AutoSize = True
        Me.SongName.Location = New System.Drawing.Point(74, 57)
        Me.SongName.Name = "SongName"
        Me.SongName.Size = New System.Drawing.Size(76, 13)
        Me.SongName.TabIndex = 0
        Me.SongName.Text = "%SongName%"
        '
        'SongArtist
        '
        Me.SongArtist.AutoSize = True
        Me.SongArtist.Location = New System.Drawing.Point(119, 57)
        Me.SongArtist.Name = "SongArtist"
        Me.SongArtist.Size = New System.Drawing.Size(71, 13)
        Me.SongArtist.TabIndex = 1
        Me.SongArtist.Text = "%SongArtist%"
        '
        'SongTime
        '
        Me.SongTime.AutoSize = True
        Me.SongTime.Location = New System.Drawing.Point(196, 57)
        Me.SongTime.Name = "SongTime"
        Me.SongTime.Size = New System.Drawing.Size(71, 13)
        Me.SongTime.TabIndex = 2
        Me.SongTime.Text = "%SongTime%"
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(800, 450)
        Me.Controls.Add(Me.SongArtist)
        Me.Controls.Add(Me.SongName)
        Me.Controls.Add(Me.SongTime)
        Me.Name = "Form1"
        Me.Text = "Form1"
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents SongName As Label
    Friend WithEvents SongArtist As Label
    Friend WithEvents SongTime As Label
End Class
