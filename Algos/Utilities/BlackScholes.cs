using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algorithms.Utilities
{
    public class BlackScholes
    {

        private double N, sigma2;
        private double d1, d2, deltaT, sd;
        private double phi1, phi2, phi3, phi4;
        private double p1, p2;

        // Translated from Pascal code found in Wikipedia article
        // https://en.wikipedia.org/wiki/Normal_distribution#Cumulative_distribution_function

        private double CDF(double x)
        {
            double sum = x, val = x;

            for (int i = 1; i <= 100; i++)
            {
                val *= x * x / (2.0 * i + 1.0);
                sum += val;
            }

            return 0.5 + (sum / Math.Sqrt(2.0 * Math.PI)) * Math.Exp(-(x * x) / 2.0);
        }






        //Blackscholes
        public double GetDelta(double S, double r, DateTime Expiry, double  X, double option_price)
        {
        //    private double N, sigma2;
        //private double d1, d2, deltaT, sd;
        //private double phi1, phi2, phi3, phi4;
        //private double p1, p2;

        double t = (Expiry - DateTime.Today).TotalDays / 365;
        double sigma = option_price_implied_volatility_call_black_scholes_newton(S, X, r, t, option_price);

        sigma2 = sigma* sigma;
            deltaT = t;// T - t;
            N = 1.0 / Math.Sqrt(2.0 * Math.PI* sigma2);
        sd = Math.Sqrt(deltaT);
            d1 = (Math.Log(S / X) + (r + 0.5 * sigma2) * deltaT) / (sigma* sd);
            d2 = d1 - sigma* sd;
        phi1 = CDF(d1);
        phi2 = CDF(d2);
        phi3 = CDF(-d2);
        phi4 = CDF(-d1);

            return phi1;
        }

    public double option_price_implied_volatility_call_black_scholes_newton(
        double S, double X, double r, double time, double option_price)
    {
        // check for arbitrage violations:
        // if price at almost zero volatility greater than price, return 0
        double sigma_low = 1e-5;
        double c, p;
        //EuropeanCall ec = new EuropeanCall(S, X, r * 0.01, sigma_low, 0, time, out c, out p);

        DeriveOptionPrice(S, X, r * 0.01, sigma_low, 0, time, out c, out p);

        double price = c;
        if (price > option_price) return 0.0;

        const int MAX_ITERATIONS = 100;
        const double ACCURACY = 1.0e-4;
        double t_sqrt = Math.Sqrt(time);

        double sigma = (option_price / S) / (0.398 * t_sqrt);    // find initial value
        for (int i = 0; i < MAX_ITERATIONS; i++)
        {
            DeriveOptionPrice(S, X, r * 0.01, sigma_low, 0, time, out c, out p);
            price = c;
            double diff = option_price - price;
            if (Math.Abs(diff) < ACCURACY) return sigma;
            double d1 = (Math.Log(S / X) + r * time) / (sigma * t_sqrt) + 0.5 * sigma * t_sqrt;
            double vega = S * t_sqrt * CDF(d1);
            sigma = sigma + diff / vega;
        };
        return 1;  // something screwy happened, should throw exception
    }

    private void DeriveOptionPrice(double S, double X, double r,
      double sigma, double t, double T, out double c, out double p)
    {
        // S = underlying asset price (stock price)
        // X = exercise price
        // r = risk free interst rate
        // sigma = standard deviation of underlying asset (stock)
        // t = current date
        // T = maturity date
        //double sigma = option_price_implied_volatility_call_black_scholes_newton(S, X, r, t, option_price);
        sigma2 = sigma * sigma;
        deltaT = T - t;
        N = 1.0 / Math.Sqrt(2.0 * Math.PI * sigma2);
        sd = Math.Sqrt(deltaT);
        d1 = (Math.Log(S / X) + (r + 0.5 * sigma2) * deltaT) / (sigma * sd);
        d2 = d1 - sigma * sd;
        phi1 = CDF(d1);
        phi2 = CDF(d2);
        phi3 = CDF(-d2);
        phi4 = CDF(-d1);
        c = S * phi1 - X * Math.Exp(-r * deltaT) * phi2;
        p = X * Math.Exp(-r * deltaT) * phi3 - S * phi4;
    }
    
















    //public  double GetDelta(double S, double X, double r,
    //        double sigma, double t, double option_price)
    //    {
    //        double sigma = option_price_implied_volatility_call_black_scholes_newton(S, X, r, t, option_price);

    //        return phi1;
    //    }
    //    public BlackScholes(double S, double X, double r,
    //        double sigma, double t, double T, out double c, out double p)
    //    {
    //        // S = underlying asset price (stock price)
    //        // X = exercise price
    //        // r = risk free interst rate
    //        // sigma = standard deviation of underlying asset (stock)
    //        // t = current date
    //        // T = maturity date
    //        double sigma = option_price_implied_volatility_call_black_scholes_newton(S, X, r, t, option_price);
    //        sigma2 = sigma * sigma;
    //        deltaT = T - t;
    //        N = 1.0 / Math.Sqrt(2.0 * Math.PI * sigma2);
    //        sd = Math.Sqrt(deltaT);
    //        d1 = (Math.Log(S / X) + (r + 0.5 * sigma2) * deltaT) / (sigma * sd);
    //        d2 = d1 - sigma * sd;
    //        phi1 = CDF(d1);
    //        phi2 = CDF(d2);
    //        phi3 = CDF(-d2);
    //        phi4 = CDF(-d1);
    //        c = S * phi1 - X * Math.Exp(-r * deltaT) * phi2;
    //        p = X * Math.Exp(-r * deltaT) * phi3 - S * phi4;
    //    }



    //    // file black_scholes_imp_vol_newt.cc
    //    // author: Bernt A Oedegaard
    //    // calculate implied volatility of Black Scholes formula using newton steps

    //    //# include "fin_algoritms.h"
    //    //# include "normdist.h"
    //    //# include <cmath>

    //    public double option_price_implied_volatility_call_black_scholes_newton(
    //         double S, double X, double r, double time, double option_price)
    //    {
    //        // check for arbitrage violations:
    //        // if price at almost zero volatility greater than price, return 0
    //        double sigma_low = 1e-5;
    //        double c, p;
    //        BlackScholes ec = new BlackScholes(S, X, r * 0.01, sigma_low, 0, time, out c, out p);

    //        double price = c;
    //        if (price > option_price) return 0.0;

    //        const int MAX_ITERATIONS = 100;
    //        const double ACCURACY = 1.0e-4;
    //        double t_sqrt = Math.Sqrt(time);

    //        double sigma = (option_price / S) / (0.398 * t_sqrt);    // find initial value
    //        for (int i = 0; i < MAX_ITERATIONS; i++)
    //        {
    //            ec = new BlackScholes(S, X, r * 0.01, sigma, 0, time, out c, out p);
    //            price = c;
    //            double diff = option_price - price;
    //            if (Math.Abs(diff) < ACCURACY) return sigma;
    //            double d1 = (Math.Log(S / X) + r * time) / (sigma * t_sqrt) + 0.5 * sigma * t_sqrt;
    //            double vega = S * t_sqrt * CDF(d1);
    //            sigma = sigma + diff / vega;
    //        };

    //        ///TODO: correct this case
    //        return -99e10;  // something screwy happened, should throw exception
    //    }
    }

}

