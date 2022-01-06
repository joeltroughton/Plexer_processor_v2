using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Diagnostics;
using WinForms = System.Windows.Forms;
using System.IO;
using System.Windows.Threading;
using System.Threading;

namespace Plexer_processor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public class Measurement
        {
            public DateTime timestamp { get; set; }
            public int unixEpoch { get; set; }
            public double jsc { get; set; }
            public double voc { get; set; }
            public double ff { get; set; }
            public double pOut { get; set; }
        }

        public class OutputFile
        {
            public List<DateTime> timestamp = new List<DateTime>();
            public List<double> jsc = new List<double>();
            public List<double> voc = new List<double>();
            public List<double> ff = new List<double>();
            public List<double> pOut = new List<double>();

        }

        string workingDirectory;
        string[] dataFilesArr;
        string[] filesToBeAveragedArr;

        float minimumFF;
        float jscCutOff;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void DirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                workingDirectory = dialog.SelectedPath;
                dataFilesArr = System.IO.Directory.GetFiles(workingDirectory, "*.txt");

                output_box.AppendText(String.Format("Selected directory: {0}", workingDirectory) + Environment.NewLine);
                output_box.AppendText(String.Format("Found files:") + Environment.NewLine);
                output_box.ScrollToEnd();
                foreach (string file in dataFilesArr)
                {
                    string filename = System.IO.Path.GetFileName(file);
                    output_box.AppendText(filename + Environment.NewLine);
                }

            }
            else
            {
                return;
            }

        }

        private void ParseButton_Click(object sender, RoutedEventArgs e)
        {
            minimumFF = float.Parse(ff_min_box.Text);
            jscCutOff = float.Parse(jsc_min_box.Text);

            output_box.AppendText(String.Format("Beginning processing") + Environment.NewLine);
            output_box.AppendText(String.Format("Minimim JSC for legitimate FF values: {0}", jscCutOff) + Environment.NewLine);
            output_box.AppendText(String.Format("Minimim legitimate FF value: {0}", minimumFF) + Environment.NewLine);
            output_box.ScrollToEnd();

            Dispatcher.Invoke(async () =>
            {
                try
                {
                    await Task.Run(() => BeginParse());
                }
                catch (Exception ex)
                {
                    //  MessageBox.Show("Error interpreting input data");
                    //  Console.WriteLine("UpdateData error - {0}", ex);
                }

            });
        }

        private async Task BeginParse()
        {
            List<string> list_files = new List<string>(dataFilesArr);

            Parallel.ForEach(
                list_files, file =>
                {
                    List<Measurement> measurements_list = new List<Measurement>();

                    var lines = File.ReadLines(file);
                    Debug.Print(file);

                    bool first_line = true;

                    string[] read_lines;
                    List<string> read_lines_list = new List<string>();

                    foreach (string line in lines)
                    {
                        if (first_line)
                        {
                            first_line = false;
                        }
                        else
                        {
                            read_lines_list.Add(line);
                        }
                    }

                    read_lines = read_lines_list.ToArray();

                    for (int i = 0; i < read_lines.Length; i++)
                    {
                        Measurement scan = new Measurement();

                        scan.timestamp = DateTime.Parse(read_lines[i].Split("\t")[0]);
                        scan.unixEpoch = int.Parse(read_lines[i].Split("\t")[1]);
                        scan.jsc = Math.Abs(double.Parse(read_lines[i].Split("\t")[2]));
                        scan.voc = double.Parse(read_lines[i].Split("\t")[3]);
                        scan.pOut = Math.Abs(double.Parse(read_lines[i].Split("\t")[5]));

                        scan.ff = double.Parse(read_lines[i].Split("\t")[4]);

                        if ((scan.jsc < jscCutOff) || (scan.ff < minimumFF) || (scan.ff > 1))
                        {
                            scan.ff = 0;
                        }

                        if (scan.voc < 0)
                        {
                            scan.voc = 0;
                        }

                        measurements_list.Add(scan);
                    }

                    List<DateTime> theDates = new List<DateTime>();

                    foreach (Measurement measurement in measurements_list)
                    {
                        //Debug.Print("Date: {0} \t VOC: {1} \t JSC: {2} \t FF: {3} \t Pout: {4}", measurement.timestamp, measurement.voc, measurement.jsc, measurement.ff, measurement.pOut);
                        theDates.Add(measurement.timestamp);
                    }

                    DateTime[] dateArr = theDates.ToArray();
                    Measurement[] measurementsArray = measurements_list.ToArray();

                    //foreach (Measurement point in measurementsArray)
                    //{
                    //    Debug.Print("{0}", point.timestamp.ToString());
                    //}

                    //DateTime startTime = new DateTime(2021, 11, 27, 00, 00, 00);
                    DateTime rounded = RoundUp(measurementsArray[1].timestamp, TimeSpan.FromMinutes(10));
                    DateTime startTime = rounded;
                    DateTime endTime = RoundUp(measurementsArray[measurementsArray.Length - 1].timestamp, TimeSpan.FromMinutes(10)); ;
                    TimeSpan experimentDuration = endTime.Subtract(startTime);
                    int experimentDurationSecs = (int)experimentDuration.TotalSeconds;
                    int experimentDurationTensOfMinutes = experimentDurationSecs / 600;


                    OutputFile output = new OutputFile();

                    for (int i = 0; i < experimentDurationTensOfMinutes - 1; i++)
                    {
                        DateTime requestedTime = startTime.AddMinutes(10 * i);
                        DateTime closest = findClosestTimestamp(dateArr, requestedTime);
                        int closest_index = findIndexOfExactTimestamp(measurementsArray, closest);

                        int secsAbove, secsBelow;
                        double jscAbove, jscBelow, vocAbove, vocBelow, ffAbove, ffBelow, poutAbove, poutBelow;

                        TimeSpan spanBelow, spanAbove;

                        spanAbove = requestedTime.Subtract(measurementsArray[closest_index - 1].timestamp);
                        spanBelow = requestedTime.Subtract(measurementsArray[closest_index].timestamp);


                        secsBelow = -(int)spanBelow.TotalSeconds;
                        secsAbove = -(int)spanAbove.TotalSeconds;

                        jscBelow = measurementsArray[closest_index - 1].jsc;
                        jscAbove = measurementsArray[closest_index].jsc;

                        vocBelow = measurementsArray[closest_index - 1].voc;
                        vocAbove = measurementsArray[closest_index].voc;

                        ffBelow = measurementsArray[closest_index - 1].ff;
                        ffAbove = measurementsArray[closest_index].ff;

                        poutBelow = measurementsArray[closest_index - 1].pOut;
                        poutAbove = measurementsArray[closest_index].pOut;


                        double jsczero = findZeroCrossing(secsBelow, secsAbove, jscBelow, jscAbove);
                        double voczero = findZeroCrossing(secsBelow, secsAbove, vocBelow, vocAbove);
                        double ffzero = findZeroCrossing(secsBelow, secsAbove, ffBelow, ffAbove);
                        double poutzero = findZeroCrossing(secsBelow, secsAbove, poutBelow, poutAbove);
                        //Debug.Print("{0} \t {1:F3} \t {2:F3} \t {3:F3} \t {4:F3}", requestedTime.ToString(), voczero, jsczero, ffzero, poutzero);
                        //Debug.Print("{0}\tClosest:{6}\t{1:F3}\tTBelow {2}\tTAbove {3}\tVbelow {4}\tVabobe {5}", requestedTime.ToString(), voczero, secsAbove, secsBelow, vocAbove, vocBelow, closest.ToString());

                        output.timestamp.Add(requestedTime.AddMinutes(-10)); // Always looking ahead by 10 mins
                        output.voc.Add(voczero);
                        output.jsc.Add(jsczero);
                        output.ff.Add(ffzero);
                        output.pOut.Add(poutzero);

                    }

                    String parsed_dir = workingDirectory + "\\" + "parsed";
                    DirectoryInfo di = Directory.CreateDirectory(parsed_dir);

                    String dataout = parsed_dir + "\\";
                    dataout = dataout + System.IO.Path.GetFileName(file);


                    using (StreamWriter outputfile = new StreamWriter(dataout))
                    {

                        DateTime[] timestampArray = output.timestamp.ToArray();
                        double[] vocArray = output.voc.ToArray();
                        double[] jscArray = output.jsc.ToArray();
                        double[] ffArray = output.ff.ToArray();
                        double[] pOutArray = output.pOut.ToArray();

                        outputfile.WriteLine("Timestamp (h), VOC (V), JSC (mA/cm2), FF, Pout (%)");

                        for (int j = 0; j < timestampArray.Length; j++)
                        {
                            string line = String.Format("{0}, {1:F3}, {2:F3}, {3:F3}, {4:F3}", timestampArray[j].ToString(), vocArray[j], jscArray[j], ffArray[j], pOutArray[j]);
                            outputfile.WriteLine(line);
                        }
                        string filename = System.IO.Path.GetFileName(dataout);
                        System.Windows.Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate
                        {
                            output_box.AppendText(String.Format("Completed processing of {0}", filename) + Environment.NewLine); output_box.ScrollToEnd();
                        });



                    }
                });

            System.Windows.Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (ThreadStart)delegate
            {
                output_box.AppendText(String.Format("Processing complete") + Environment.NewLine); output_box.ScrollToEnd();
            });
        }


        static double findZeroCrossing(double lowerTimespan, double upperTimespan, double lowerTVal, double upperTVal)
        {
            double slope = (upperTVal - lowerTVal) / (Math.Abs(upperTimespan) + Math.Abs(lowerTimespan));
            double zeroCrossing = (lowerTVal) - (slope * lowerTimespan);
            //Debug.Print("Slope: {0} Crossing: {1}", slope, zeroCrossing);

            return zeroCrossing;
        }

        static int findIndexOfExactTimestamp(Measurement[] array, DateTime timeToFind)
        {
            int closest_index = 0;

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].timestamp == timeToFind)
                {
                    closest_index = i;
                }
            }
            return closest_index;
        }

        static DateTime findClosestTimestamp(DateTime[] array, DateTime timeToFind)
        {

            DateTime closestDate = new DateTime(2020, 1, 1);

            long min = Math.Abs(timeToFind.Ticks - array[0].Ticks);
            long diff;
            foreach (DateTime date in array)
            {
                diff = Math.Abs(timeToFind.Ticks - date.Ticks);
                if (diff < min)
                {
                    min = diff;
                    closestDate = date;
                }
            }

            return closestDate;
        }
        static DateTime RoundUp(DateTime dt, TimeSpan d)
        {
            return new DateTime((dt.Ticks + d.Ticks - 1) / d.Ticks * d.Ticks, dt.Kind);
        }

        public void GenerateAveragedData()
        {
            String parsedFileDir = workingDirectory + "\\" + "parsed";

            // Read each .txt file into a class
            List<Measurement> averaged_measurements_list = new List<Measurement>();
            foreach (string file in filesToBeAveragedArr)
            {

                var lines = File.ReadLines(file);
                Debug.Print(file);

                bool first_line = true;

                string[] read_lines;
                List<string> read_lines_list = new List<string>();

                foreach (string line in lines)
                {
                    if (first_line)
                    {
                        first_line = false;
                    }
                    else
                    {
                        read_lines_list.Add(line);
                    }
                }

                read_lines = read_lines_list.ToArray();

                for (int i = 0; i < read_lines.Length; i++)
                {
                    Measurement scan = new Measurement();

                    scan.timestamp = DateTime.Parse(read_lines[i].Split("\t")[0]);
                    scan.jsc = Math.Abs(double.Parse(read_lines[i].Split("\t")[2]));
                    scan.voc = double.Parse(read_lines[i].Split("\t")[1]);
                    scan.pOut = Math.Abs(double.Parse(read_lines[i].Split("\t")[4]));
                    scan.ff = double.Parse(read_lines[i].Split("\t")[3]);


                    averaged_measurements_list.Add(scan);
                }
            }

            // Run through and take the average of each line in each measurement


        }

        private void AverageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "All Files *_param.txt | *_param.txt";
            open.Multiselect = true;
            open.Title = "Select files to average";

            if (open.ShowDialog() == WinForms.DialogResult.OK)
            {
                filesToBeAveragedArr = open.FileNames;
            }
            else
            {
                return;
            }
        }
    }
}
