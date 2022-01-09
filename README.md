# Plexer processor
A handy app to process and visualise data from OMEGALab Plexer multichannel PV hardware.

<p align="center">
  <img src="https://github.com/joeltroughton/Plexer_processor_v2/raw/master/plexerprocessor.png" width="600" title="Plexer Processor">
</p>

## Processing
- Interpolate JV data over set timespans to generate easy-to-visualise graphs
  - Instead of entries at 16:58:05 and 17:09:21, data can be linearly interpolated to give entries at 17:00:00 and 17:10:00
- Discard junk data
  - Correct for errors in JV acuqisition. Eg. Fill factor > 1.0
- Low-light data-cut
  - VOC and FF measurements become unreliable in low-light conditions. Options are provided to discard data at low Jscs
- Option to generate an average of pixel JV data
  - All files must have the same start time and number of entries. Manually trim any extra data if nescesarry
  - Output file placed in the same directory as input files (with _average.txt suffix)
## Visualisation
- Graphs rendered of all files inside working folder
