﻿' ################################################################################
' #                             EMBER MEDIA MANAGER                              #
' ################################################################################
' ################################################################################
' # This file is part of Ember Media Manager.                                    #
' #                                                                              #
' # Ember Media Manager is free software: you can redistribute it and/or modify  #
' # it under the terms of the GNU General Public License as published by         #
' # the Free Software Foundation, either version 3 of the License, or            #
' # (at your option) any later version.                                          #
' #                                                                              #
' # Ember Media Manager is distributed in the hope that it will be useful,       #
' # but WITHOUT ANY WARRANTY; without even the implied warranty of               #
' # MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the                #
' # GNU General Public License for more details.                                 #
' #                                                                              #
' # You should have received a copy of the GNU General Public License            #
' # along with Ember Media Manager.  If not, see <http://www.gnu.org/licenses/>. #
' ################################################################################

Imports System.Text.RegularExpressions
Imports EmberAPI
Imports NLog

Public Class IMDBTrailer

#Region "Fields"
    Shared logger As Logger = NLog.LogManager.GetCurrentClassLogger()

    Private IMDBID As String
    Private _trailerlist As New List(Of Trailers)

#End Region 'Fields

#Region "Constructors"

    Public Sub New(ByVal sIMDBID As String)
        Clear()
        IMDBID = sIMDBID
        GetMovieTrailers()
    End Sub

#End Region 'Constructors

#Region "Properties"

    Public Property TrailerList() As List(Of Trailers)
        Get
            Return _trailerlist
        End Get
        Set(ByVal value As List(Of Trailers))
            _trailerlist = value
        End Set
    End Property

#End Region 'Properties

#Region "Methods"

    Private Sub Clear()
        _trailerlist = New List(Of Trailers)
    End Sub

    Private Sub GetMovieTrailers()

        Dim BaseURL As String = Master.eSettings.MovieIMDBURL
        Dim SearchURL As String

        Dim TrailerNumber As Integer = 0
        Dim Trailers As MatchCollection
        Dim Qualities As MatchCollection
        Dim trailerPage As String
        Dim trailerUrl As String
        Dim trailerTitle As String
        Dim Link As Match
        Dim currPage As Integer = 0
        Dim _ImdbTrailerPage As String = String.Empty
        Dim sHTTP As New HTTP

        Try
            If Not String.IsNullOrEmpty(IMDBID) Then
                Dim pPattern As String = "of [0-9]{1,3}"                            'Trailer page # of #
                Dim tPattern As String = "imdb/(vi[0-9]+)/"                         'Specific trailer website
                Dim nPattern As String = "<title>.*?\((?<TITLE>.*?)\).*?</title>"   'Trailer title inside brakets
                Dim mPattern As String = "<title>(?<TITLE>.*?)</title>"             'Trailer title without brakets
                Dim qPattern As String = "imdb/single\?format=([0-9]+)p"            'Trailer qualities

                SearchURL = String.Concat("http://", BaseURL, "/title/tt", IMDBID, "/videogallery/content_type-Trailer") 'IMDb trailer website of a specific movie, filtered by trailers only

                'download trailer website
                _ImdbTrailerPage = sHTTP.DownloadData(SearchURL)
                sHTTP = Nothing

                If _ImdbTrailerPage.ToLower.Contains("page not found") Then
                    _ImdbTrailerPage = String.Empty
                End If

                If Not String.IsNullOrEmpty(_ImdbTrailerPage) Then
                    'check if more than one page exist
                    Link = Regex.Match(_ImdbTrailerPage, pPattern)

                    If Link.Success Then
                        TrailerNumber = Convert.ToInt32(Link.Value.Substring(3))

                        If TrailerNumber > 0 Then
                            currPage = Convert.ToInt32(Math.Ceiling(TrailerNumber / 10))

                            For i As Integer = 1 To currPage
                                If Not i = 1 Then
                                    sHTTP = New HTTP
                                    _ImdbTrailerPage = sHTTP.DownloadData(String.Concat(SearchURL, "?page=", i))
                                    sHTTP = Nothing
                                End If

                                'search all trailer on trailer website
                                Trailers = Regex.Matches(_ImdbTrailerPage, tPattern)
                                Dim linksCollection As String() = From m As Object In Trailers Select CType(m, Match).Value Distinct.ToArray()

                                For Each trailer As String In linksCollection
                                    'go to specific trailer website
                                    sHTTP = New HTTP
                                    trailerPage = sHTTP.DownloadData(String.Concat("http://", BaseURL, "/video/", trailer))
                                    sHTTP = Nothing
                                    trailerTitle = Regex.Match(trailerPage, nPattern).Groups(1).Value.ToString.Trim
                                    If String.IsNullOrEmpty(trailerTitle) Then
                                        trailerTitle = Regex.Match(trailerPage, mPattern).Groups(1).Value.ToString.Trim
                                        trailerTitle = trailerTitle.Replace("- IMDb", String.Empty).Trim
                                    End If
                                    'get all qualities of a specific trailer
                                    Qualities = Regex.Matches(trailerPage, String.Concat("http://www.imdb.com/video/", trailer, qPattern))
                                    Dim trailerCollection As String() = From m As Object In Qualities Select CType(m, Match).Value Distinct.ToArray()

                                    'get all download URLs of a specific trailer
                                    For Each qual As String In trailerCollection
                                        sHTTP = New HTTP
                                        Dim QualityPage As String = sHTTP.DownloadData(qual)
                                        sHTTP = Nothing
                                        Dim QualLink As Match = Regex.Match(QualityPage, "videoPlayerObject.*?viconst")
                                        Dim dowloadURL As MatchCollection = Regex.Matches(QualLink.Value, "ffname"":""(?<QUAL>.*?)"",""height.*?url"":""(?<LINK>.*?)""")
                                        Dim Resolution As String = dowloadURL.Item(0).Groups(1).Value
                                        trailerUrl = dowloadURL.Item(0).Groups(2).Value
                                        Dim Res As Enums.TrailerQuality

                                        Select Case Resolution
                                            Case "SD"
                                                Res = Enums.TrailerQuality.SQ240p
                                            Case "480p"
                                                Res = Enums.TrailerQuality.HQ480p
                                            Case "720p"
                                                Res = Enums.TrailerQuality.HD720p
                                            Case Else
                                                Res = Enums.TrailerQuality.OTHERS
                                        End Select

                                        _trailerlist.Add(New Trailers With {.URL = trailerUrl, .Description = trailerTitle, .WebURL = String.Concat("http://", BaseURL, "/video/", trailer), .Resolution = Res})
                                    Next
                                Next
                            Next
                        End If

                    End If
                End If
            End If

        Catch ex As Exception
            logger.Error(New StackFrame().GetMethod().Name, ex)
        End Try

    End Sub

#End Region 'Methods

End Class