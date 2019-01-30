# o2jam_converter

Just a couple of utilities designed to make working with o2jam files easier. Currently an implementation for dumping ojm files (as per the ojm documentation on the open2jam blogspot) and also some work on ojn headers. 

This currently has working osz conversion for OGG only o2jam files (which is most of the files anyways). There currently isn't an interface for this, so you'll have to put your conversion directory manually in the debug file.

## Usage

Currently the program has a CLI interface. You can use it by the following syntax. 

If you want to use the audio renderer, you will need the external render-ojn program for generating audio files. This enables previews in the Osu! beatmap selection. Place it in the same directory as this project's binary.

```
O2JamDebug.exe [ojn/ojm folder path] [output path]
```
