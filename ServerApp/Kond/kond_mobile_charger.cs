﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ns.graph;
using System.Timers;
using System.Collections;
using System.IO;
//using ns.wrsn;
namespace Kond

{
   
    public enum Mc_Status
    {
        stationary=0,
        charging=1,
        travelling=2,

    };
    public class mobile_charger : ns_node
    {      
        

        private PriorityQueue<double, ns_node> ChargingQ;
        private ns_point old_location;
        public Mc_Status MCStatus = new Mc_Status();
        private Timer ChargeProgressTimer;
        private double ChargeTimerInterval = 30;//*1000;// 30 secc;
        private ns_node current_charged_node;
        private double c_time = 0;
        private int tours_count = 0;
        private int number_of_charged_node = 0;
        private DataLogger dead_nodes;
        private DataLogger queue_len;
        // the travelling power cost
        private double mc_consumed_power_per_m = 0.05; //
        private charge_statistics stats = new charge_statistics();
        
        public mobile_charger(string mob_id ) : base(mob_id)
        {

            this.ChargingQ = new PriorityQueue<double, ns_node>(Compare);
            this.NodeType = Node_TypeEnum.Mobile_Charger;
            this.MCStatus = Mc_Status.stationary;
            this.old_location = this.location;
            this.current_charged_node = new ns_node();
            this.__init_charger_Timer();
            this.c_time = 0;
            this.tours_count = 0;
            __init__();
        }

        public mobile_charger(string mob_id , double x, double y) : base(mob_id , x , y)
        {

            this.ChargingQ = new PriorityQueue<double, ns_node>(Compare);
            this.MCStatus = Mc_Status.stationary;
            this.NodeType = Node_TypeEnum.Mobile_Charger;

            this.old_location = this.location;
            this.current_charged_node = new ns_node();
            this.__init_charger_Timer();
            this.c_time = 0;
            this.tours_count = 0;
            __init__();
        }
        public mobile_charger(string mob_id, ns_point loc)
            : base(mob_id, loc)
        {

            this.ChargingQ = new PriorityQueue<double, ns_node>(Compare);
            this.NodeType = Node_TypeEnum.Mobile_Charger;
            this.MCStatus = Mc_Status.stationary;
            this.old_location = this.location;
            this.__init_charger_Timer();
            this.current_charged_node = new ns_node();
            this.c_time = 0;
            this.tours_count = 0;
            __init__();
        }
       
        private void __init_charger_Timer()
        {
            this.ChargeProgressTimer = new Timer(1*1000);
            this.ChargeProgressTimer.Elapsed += ChargeProgressTimer_Elapsed;
        }
       private double getChargeTimerInterval()
        {
          // this.current_charged_node.PowerConfig.m
            double residual_bat = this.current_charged_node.PowerConfig.BatteryCapacity;
            double charge_rate = this.current_charged_node.PowerConfig.ChargeRate;
            double max_batt = this.current_charged_node.PowerConfig.getMaxBatteryCapacity();
            double charge_time = Math.Abs(max_batt - residual_bat) / charge_rate;
            //printfx("E_r:{0} , E_max:{1} C_t:{2} S_n:{3}", residual_bat, max_batt, charge_time, this.current_charged_node.tag);
            this.ChargeTimerInterval = (int)(charge_time);
          // printfx("charge_time:{0} of node {1}", this.ChargeTimerInterval, this.current_charged_node.tag);
            return this.ChargeTimerInterval;
        }
      // private bool do_partial = false;
       
        
       
        public double optimization_constant = 4;
        private double get_adaptable_time(int count, double er, double cr, double emax)
        {
            if(count>0)
            {
                double fx = ((emax - er) / cr) * Math.Exp(-count / this.optimization_constant);
                printfx("fx_time:{0}, opconst:{1}\n", fx, this.optimization_constant);
               /*if (fx < 10) 
                {
                    emax *= 0.50;
                    return (emax - er) / cr;
                }*/
               return fx;
            }
            if (count <= 5) return (emax - er) / cr;
            
            if (count <= 10)
            {
                emax *= 0.90;
                return (emax - er) / cr;
            }
            
            if (count <= 15)
            {
                emax *= 0.85;
                return (emax - er) / cr; 
            }


            if (count <= 20)
            {
                emax *= 0.75;
                return (emax - er) / cr;
            }
            
            if (count <= 25)
            {
                emax *= 0.60;
                return (emax - er) / cr;
            }
            emax *= 0.50;
            return (emax - er) / cr;



        }

        private double calc_average_charging_time()
        {

            int count = this.ChargingQ.size();
            double residual_bat = this.current_charged_node.PowerConfig.BatteryCapacity;
            double charge_rate = this.current_charged_node.PowerConfig.ChargeRate;
            double max_batt = this.current_charged_node.PowerConfig.getMaxBatteryCapacity();
            double time = get_adaptable_time(count, residual_bat, charge_rate, max_batt);
            printfx("charge time:{0}", time);
            return time;
            
        }
        
        public void calc_power_consumed_per_distance(double distance)
        {
           double p = distance * this.mc_consumed_power_per_m;
           this.PowerConfig.BatteryCapacity -= p;
        }
        public void calc_power_consumed_in_charging()
        {
            this.PowerConfig.BatteryCapacity -= this.PowerConfig.ChargeRate;
        }

        private void ChargeProgressTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {

                this.calc_power_consumed_in_charging();

                if (this.c_time >= this.ChargeTimerInterval)
                {
                    this.c_time = 0;
                    this.ChargeProgressTimer.Stop();
                    this.StopChargerTimer();
                    this.number_of_charged_node++;
                    printfx("Time's up:{0}\n", this.ChargeTimerInterval);

                    return;
                }
                this.c_time += 1;
                // if the reach max capacity stop the timer.
                if(this.current_charged_node.ChargingInProcess())
                {
                    this.c_time = 0;
                    this.number_of_charged_node++;
                    this.ChargeProgressTimer.Stop();
                    this.StopChargerTimer();
                    return;
                }
                // the power consumed during charging process.
                

            }catch (Exception ex)
            {
                printfx("ex at Timer:{0} in {1}\n", ex.Message, "ChargeProgressTimer_Elapsed");
                printfx("InnerEx:{0}, Souce:{1}\n", ex.InnerException, ex.Source);
            }
           
        }
        public int GetQueueLen()
        {
            return this.ChargingQ.size();
        }
        public int getNumberOfChargedNodes()
        {
            int ret = this.number_of_charged_node;
            this.number_of_charged_node = 0;
            return ret;
        }




        // //This is NJNP (implement my own priority algorithm based on AHP)      
        // private void updatePriority()
        // {

        //     double count = 0;

        //     //for NJNP algorithm and implementation base on the shortest distance
        //     this.ChargingQ.Foreach((item) =>
        //         {
        //             double w_time = this.stats.Get(item.Value).getWaitingTime();
        //             var distance = this.edistance(item.Value); //get shortest distance and charge the sensor

        //             //Console.WriteLine("Shortest Distance is {0}",px);

        //             item.priority = distance;
        //             count++;

        //             //printfx("id:{0},prio:{1},wtime:{2}", item.Value.tag, item.priority, w_time);

        //             });
        //     //this.dead_nodes.Writeline(count);
        //}

        //implement my own priority algorithm based on AHP    

        string file_path = @"D:\Research\Code\WSN_SIM\simulation_results\results.csv";
        private void updatePriority()
        {
            Hashtable hashtable = new Hashtable();
            var dictionary = new Dictionary<string, Tuple<double, double, double, double>>();
            double count = 0;

            //for NJNP algorithm and implementation base on the shortest distance
            this.ChargingQ.Foreach((item) =>
            {
                double w_time = this.stats.Get(item.Value).getWaitingTime();
                var distance = this.edistance(item.Value); //get shortest distance and charge the sensor

                //Console.WriteLine("Shortest Distance is {0}",px);

                var sensor_id = item.Value.tag;
                var residual_energy = item.Value.PowerConfig.BatteryCapacity;
                var charge_rate = item.Value.PowerConfig.ChargeRate;
                var consumption_rate = item.Value.PowerConfig.energy_consumptionRate;

                var attribute_tuple = new Tuple<double, double, double, double>(residual_energy, distance, charge_rate, consumption_rate);

                dictionary.Add(sensor_id, attribute_tuple);



                string new_data = Convert.ToString(sensor_id) +" "+ Convert.ToString(distance) +" "+ Convert.ToString(residual_energy);

                using (StreamWriter output_file = new StreamWriter(file_path, true))
                {
                    output_file.WriteLine(new_data);
                }

                Console.WriteLine("ID: {0}. Distance: {1}, Residual Energy: {2}", sensor_id, distance, residual_energy);




                item.priority = distance;
                count++;

                //printfx("id:{0},prio:{1},wtime:{2}", item.Value.tag, item.priority, w_time);

                //hashtable.Add(item.Value.tag, this.edistance(item.Value));
                //Console.WriteLine("Sensor id: {0}, Priority - Distance: {1}, Waiting Time {2}, Residual Energy: {3}", item.Value.tag, item.priority, w_time, item.Value.PowerConfig.BatteryCapacity);
            });
            //this.dead_nodes.Writeline(count);

            foreach (var ky in dictionary)
            {
                Console.WriteLine("Dictionary: Key{0}", ky);
            }

        }



        /// <summary>
        /// while charging check if there is a node whose power less
        /// than the current node and the power of the current is greater than e.g 50%
        /// then quit charge and go to charge the node with min
        /// </summary>
        /// <returns> false --> to continue charging the current node
        /// true --> quit charging and go to charge the node with minimum power.
        /// </returns>
        private bool ShouldQuit()
        {
            int qsize = this.ChargingQ.size();
            //if (qsize == 1) return false;
            //updatePriority();
            ns_node current = this.current_charged_node;
            double power = current.PowerConfig.BatteryCapacity;
            bool ret = false;
            this.ChargingQ.Foreach((n) =>
                {
                    if(current.tag != n.Value.tag)
                    {
                        double np = n.Value.PowerConfig.BatteryCapacity;
                        ret |= (np < power);
                    }
                });

            return ret && (power >= 60);
        }
        public delegate void __OnChargeNodeDone(mobile_charger mc, ns_node_stats service_stats);
        public event __OnChargeNodeDone OnNodeChargeDoneStats;
        private void printOutStatistics(ns_node node)
        {
           var node_stats =  this.stats.Get(node);
           node_stats.queue_len_at = this.ChargingQ.size();
            if(this.OnNodeChargeDoneStats!=null)
            {
                this.OnNodeChargeDoneStats(this, node_stats);
            }
        }
        private string report_folder;
        public void SetReportFolder(string folder)
        {
            this.report_folder = folder;
        }
        public bool enable_threshold_switch = false;
        private void StopChargerTimer()
        {
            try
            {
                printfx("Timer Stopped {0}", this.tag);
                this.MCStatus = Mc_Status.stationary;

                this.current_charged_node.set_charge_request_toggle();
                this.current_charged_node.NotifyChargeRequestReceived(false);
                
                this.c_time = 0;
                this.stats.Get(current_charged_node).SetChargingEndTimeNow();
                printOutStatistics(this.current_charged_node);
                if (!this.ChargingQ.IsEmpty())
                {
                    updatePriority();// very important.
                    go_to_charge();
                    //this.MCStatus = Mc_Status.travelling;
                }
                else
                {
                    if (this.ChargeProgressTimer != null)
                    {
                        this.ChargeProgressTimer.Stop();
                    }
                }
            } catch(Exception ex)
            {
                printfx("Exception at :{0} @StopTimer", ex.Message);
            }
        }
       
        private void StartChargeTimer()
        {
           
            this.ChargeTimerInterval = this.getChargeTimerInterval();
            
            this.MCStatus = Mc_Status.charging;
            this.stats.Get(this.current_charged_node).SetChargingStartTimeNow();
            if (this.ChargeProgressTimer != null)
            {
                //this.charg_timer_running = true;
                this.ChargeProgressTimer.Start();
                printfx("Timer Start {0}", this.tag);
            }
            else
            {
                printfx("Timer is null:{0}",this.tag);
            }
        }
       
        private bool Compare(PriorityValuePair<double, ns_node> p1, PriorityValuePair<double, ns_node> p2)
        {
            return p1.priority <= p2.priority;
        }
        /// <summary>
        /// get the sensor node with the highest priority .
        /// 
        /// </summary>
        /// <returns></returns>
         
        public void create_stats(ns_node node)
        {
            ns_node_stats stats = new ns_node_stats(node);
            stats.SetChargeReqTimeNow();
            node.FirstChargeRequestTime(true);// set the charge request time.
            this.stats.Add_item(node.tag, stats);
        }
        private void push_charging_path_mission(List<ns_node> path)
        {
            foreach(var from in path)
            {
                if (!this.Contains(from))
                {
                    double prio = this.mdistance(from) * from.PowerConfig.BatteryCapacity / from.PowerConfig.energy_consumptionRate;
                    this.ChargingQ.Enqueue(from, prio);
                    create_stats(from);
                }
            }
            if (this.MCStatus == Mc_Status.stationary)
            {
                go_to_charge();
                this.MCStatus = Mc_Status.travelling;
            }
        }
        public override void OnPacketReceived(ns.graph.net_packet packet , ns_node from )
        {
           
            if (packet.final_dst != this.tag) return;
            int count = this.ChargingQ.size();
           
            if (packet.Type == packetType.ChargeRequest)
            {
                
                if(from.tag != packet.src_addr)
                {
                   
                    from = graph_algorithms.Dijkstra_GetNode(from, packet.src_addr);
                   
                }
                //int count = this.ChargingQ.size();
                this.PowerConfig.charge_request_packets_rx += 1;
                from.NotifyChargeRequestReceived(true);
                printfx("{0}:Charge Request from {1} qlen:{2}", this.tag, from.tag, count);
               
                if (!this.Contains(from))
                {
                    //if(!Check(from))
                    
                    double prio = this.edistance(from);
                    this.ChargingQ.Enqueue(from, prio);
                    create_stats(from);
                   
                   // queue_len.AppendData(count);
                }

                if (this.MCStatus == Mc_Status.stationary)
                {
                    go_to_charge();
                    this.MCStatus = Mc_Status.travelling;
                }
            }
           
        }
        public bool Contains(ns_node from)
        {
            return this.ChargingQ.Contains((p) =>
                {
                    return p.Value.tag == from.tag;
                });
        }
        public delegate void OnMCvTravelling(mobile_charger mcv);
        private OnMCvTravelling TravelHandler;
        public void SetTravelHandler(OnMCvTravelling handler)
        {
            this.TravelHandler = handler;
        }
        private bool go_back_home = false;
        /// <summary>
        ///  select th node to be charge , your decision making here.
        /// </summary>
        /// <returns></returns>
        public ns_node getNodeToBeCharge()
        {
            
            var node = this.ChargingQ.First();// FCFS /// first .PR Dequeue();
            //var node = this.ChargingQ.
            printfx("Node to be Charge :{0} , priority:{1}", node.Value.tag, node.priority);

            this.current_charged_node = node.Value;
           return node.Value;
        }
        /// <summary>
        /// when the charger reach the correponding node , it calls the function.
        /// </summary>
        /// <param name="node"></param>
        public void SetCurrentChargeNode(ns_node node)
        {
            this.current_charged_node = node;
            this.stats.Get(node).SetChargerReachTimeNow();
        }
        public ns_point getOriginLocation()
        {
            return this.old_location;
        }
        public bool isBackhome()
        {
            return this.go_back_home;
        }
       public void go_to_charge()
        {
            this.tours_count++;
            this.TravelHandler(this);
            
        }
        public int getTourCount()
       {
           return this.tours_count;
       }
       public override bool ChargingInProcess()
       {
         
           StartChargeTimer();
           return true;
          
       }
        public charge_statistics get_statistics()
       {
           return this.stats;
       }
       private void __init__()
       {
           Random r = new Random(DateTime.Now.Millisecond);
           this.PowerConfig.Comm_Range = 100;
           this.PowerConfig.initial_energy = 8500;
           this.PowerConfig.PacketEnergy = 0.0001;
           this.PowerConfig.energy_consumptionRate = 0.0003;
           this.PowerConfig.energy_threshold = r.Next(30, 60);
           //this.SensingRadius = 3;
           this.PowerConfig.Temperature = r.Next(20, 40);
           this.PowerConfig.BatteryCapacity = 8500;
           this.PowerConfig.BatteryDischargeRate = 0;
           this.PowerConfig.ChargeRate = 3;
           this.PowerConfig.Sleep_mode_En = r.NextDouble() * 0.1;
           string fn_name = string.Format("{0}_dead_nodes.dat", this.tag);
           string q_fn = string.Format("{0}_queue_len.dat", this.tag);
           this.dead_nodes = new DataLogger(fn_name);
           this.queue_len = new DataLogger(q_fn, true);
          // this.max_battery_capacity = 100;
       }

    }
}
